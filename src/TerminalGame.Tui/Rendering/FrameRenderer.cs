using System.Globalization;
using System.Text;

namespace TerminalGame.Tui.Rendering;

/// <summary>
/// Turns a finished frame (<see cref="ScreenBuffer"/>) into the minimal ANSI byte stream that makes the
/// terminal show it. It remembers what the terminal currently displays (the "front" buffer) and emits only
/// the cells that changed, with a cursor jump per run and an SGR change only when the style actually differs.
/// </summary>
public sealed class FrameRenderer
{
    private const string csi = "\u001b[";

    private readonly ColorMode colorMode;
    private readonly StringBuilder output = new();
    private ScreenBuffer front = new(0, 0);
    private bool needsFullRedraw = true;

    public FrameRenderer(ColorMode colorMode)
    {
        this.colorMode = colorMode;
    }

    /// <summary>Forget what the terminal shows; the next frame repaints everything (e.g. after a resize).</summary>
    public void invalidate() => needsFullRedraw = true;

    /// <summary>Returns the escape sequence for the frame, or an empty string if nothing changed.</summary>
    public string render(ScreenBuffer frame)
    {
        output.Clear();

        if (needsFullRedraw || front.width != frame.width || front.height != frame.height)
        {
            front.resize(frame.width, frame.height);
            for (int y = 0; y < front.height; y++)
            {
                for (int x = 0; x < front.width; x++)
                {
                    front.setRaw(x, y, Cell.unknown);
                }
            }

            output.Append(csi).Append("0m").Append(csi).Append("2J");
            needsFullRedraw = false;
        }

        int cursorX = -1;
        int cursorY = -1;
        Style? current = null;

        for (int y = 0; y < frame.height; y++)
        {
            for (int x = 0; x < frame.width; x++)
            {
                Cell cell = frame.get(x, y);
                if (cell.isContinuation || cell == front.get(x, y))
                {
                    continue;
                }

                if (cursorX != x || cursorY != y)
                {
                    output.Append(csi).Append((y + 1).ToString(CultureInfo.InvariantCulture)).Append(';')
                        .Append((x + 1).ToString(CultureInfo.InvariantCulture)).Append('H');
                }

                Style style = colorMode == ColorMode.None
                    ? new Style(Color.terminalDefault, Color.terminalDefault, cell.style.attributes)
                    : cell.style;
                if (current != style)
                {
                    appendStyle(current, style);
                    current = style;
                }

                output.Append(cell.glyph);
                front.setRaw(x, y, cell);

                // A wide glyph moves the terminal cursor two columns; its continuation cell needs no output.
                bool wide = x + 1 < frame.width && frame.get(x + 1, y).isContinuation;
                if (wide)
                {
                    front.setRaw(x + 1, y, frame.get(x + 1, y));
                }

                cursorX = x + (wide ? 2 : 1);
                cursorY = y;
            }
        }

        if (output.Length == 0)
        {
            return string.Empty;
        }

        output.Append(csi).Append("0m");
        // Synchronized output (DEC 2026): the terminal applies the whole frame atomically, avoiding tearing.
        // Terminals that do not know the mode ignore it.
        return csi + "?2026h" + output + csi + "?2026l";
    }

    private void appendStyle(Style? previous, Style next)
    {
        bool attributesRemoved = previous is null || (previous.Value.attributes & ~next.attributes) != 0;
        if (attributesRemoved)
        {
            // Full reset, then re-apply everything.
            output.Append(csi).Append('0');
            appendAttributes(next.attributes);
            appendColor(next.foreground, isBackground: false);
            appendColor(next.background, isBackground: true);
            output.Append('m');
            return;
        }

        Style before = previous!.Value;
        output.Append(csi);
        bool first = true;

        void separator()
        {
            if (!first)
            {
                output.Append(';');
            }

            first = false;
        }

        TextAttributes added = next.attributes & ~before.attributes;
        if (added != TextAttributes.None)
        {
            separator();
            appendAttributeCodes(added);
        }

        if (before.foreground != next.foreground)
        {
            separator();
            appendColorCode(next.foreground, isBackground: false);
        }

        if (before.background != next.background)
        {
            separator();
            appendColorCode(next.background, isBackground: true);
        }

        output.Append('m');
    }

    private void appendAttributes(TextAttributes attributes)
    {
        if (attributes == TextAttributes.None)
        {
            return;
        }

        output.Append(';');
        appendAttributeCodes(attributes);
    }

    private void appendAttributeCodes(TextAttributes attributes)
    {
        bool first = true;
        void code(TextAttributes flag, string value)
        {
            if ((attributes & flag) == 0)
            {
                return;
            }

            if (!first)
            {
                output.Append(';');
            }

            first = false;
            output.Append(value);
        }

        code(TextAttributes.Bold, "1");
        code(TextAttributes.Dim, "2");
        code(TextAttributes.Italic, "3");
        code(TextAttributes.Underline, "4");
        code(TextAttributes.Blink, "5");
        code(TextAttributes.Reverse, "7");
        code(TextAttributes.Strikethrough, "9");
    }

    // Used after a reset: default colors need no code at all.
    private void appendColor(Color color, bool isBackground)
    {
        if (color.isDefault)
        {
            return;
        }

        output.Append(';');
        appendColorCode(color, isBackground);
    }

    private void appendColorCode(Color color, bool isBackground)
    {
        if (color.isDefault)
        {
            output.Append(isBackground ? "49" : "39");
            return;
        }

        switch (colorMode)
        {
            case ColorMode.TrueColor:
                output.Append(isBackground ? "48;2;" : "38;2;")
                    .Append(color.r.ToString(CultureInfo.InvariantCulture)).Append(';')
                    .Append(color.g.ToString(CultureInfo.InvariantCulture)).Append(';')
                    .Append(color.b.ToString(CultureInfo.InvariantCulture));
                break;
            case ColorMode.Ansi256:
                output.Append(isBackground ? "48;5;" : "38;5;")
                    .Append(ColorQuantizer.toAnsi256(color).ToString(CultureInfo.InvariantCulture));
                break;
            default:
                int index = ColorQuantizer.toAnsi16(color);
                int baseCode = index < 8 ? (isBackground ? 40 : 30) : (isBackground ? 100 : 90);
                output.Append((baseCode + index % 8).ToString(CultureInfo.InvariantCulture));
                break;
        }
    }
}
