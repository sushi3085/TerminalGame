using System.Globalization;
using System.Text;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Backends;

/// <summary>
/// A deliberately small terminal emulator: it understands exactly the escape sequences
/// <see cref="FrameRenderer"/> produces (cursor positioning, erase screen, SGR, private modes ignored) and
/// replays them onto a <see cref="ScreenBuffer"/>. Feeding a renderer's output through it and comparing the
/// result to the frame that was rendered is the framework's main correctness check, and lets games be
/// tested without a real terminal.
/// </summary>
public sealed class VtScreen
{
    private int cursorX;
    private int cursorY;
    private Style pen = Style.terminalDefault;

    public VtScreen(int width, int height)
    {
        buffer = new ScreenBuffer(width, height);
    }

    public ScreenBuffer buffer { get; private set; }

    public void resize(int width, int height)
    {
        buffer = new ScreenBuffer(width, height);
        cursorX = 0;
        cursorY = 0;
    }

    public void write(string data)
    {
        int i = 0;
        while (i < data.Length)
        {
            if (data[i] == '\u001b')
            {
                i = consumeEscape(data, i);
                continue;
            }

            int end = data.IndexOf('\u001b', i);
            if (end < 0)
            {
                end = data.Length;
            }

            writeText(data.Substring(i, end - i));
            i = end;
        }
    }

    /// <summary>The screen as text, one line per row, trailing spaces trimmed.</summary>
    public string toText()
    {
        StringBuilder builder = new();
        for (int y = 0; y < buffer.height; y++)
        {
            builder.Append(buffer.rowToString(y).TrimEnd()).Append('\n');
        }

        return builder.ToString();
    }

    private int consumeEscape(string data, int start)
    {
        int i = start + 1;
        if (i >= data.Length || data[i] != '[')
        {
            return Math.Min(i + 1, data.Length); // not a CSI sequence: skip ESC and one byte
        }

        i++;
        int parametersStart = i;
        while (i < data.Length && data[i] >= 0x20 && data[i] <= 0x3F)
        {
            i++;
        }

        if (i >= data.Length)
        {
            return data.Length;
        }

        string parameters = data.Substring(parametersStart, i - parametersStart);
        char finalByte = data[i];
        handleCsi(parameters, finalByte);
        return i + 1;
    }

    private void handleCsi(string parameters, char finalByte)
    {
        if (parameters.StartsWith('?'))
        {
            return; // private modes (alt screen, cursor, wrap, sync): irrelevant to the buffer
        }

        int[] values = parameters.Split(';').Select(p => int.TryParse(p, NumberStyles.None, CultureInfo.InvariantCulture, out int v) ? v : -1).ToArray();
        int first(int fallback) => values.Length > 0 && values[0] >= 0 ? values[0] : fallback;

        switch (finalByte)
        {
            case 'H':
            case 'f':
                cursorY = Math.Clamp(first(1) - 1, 0, Math.Max(0, buffer.height - 1));
                cursorX = Math.Clamp((values.Length > 1 && values[1] >= 0 ? values[1] : 1) - 1, 0, Math.Max(0, buffer.width - 1));
                break;
            case 'J':
                if (first(0) == 2)
                {
                    buffer.clear(Style.terminalDefault);
                }

                break;
            case 'A':
                cursorY = Math.Max(0, cursorY - first(1));
                break;
            case 'B':
                cursorY = Math.Min(buffer.height - 1, cursorY + first(1));
                break;
            case 'C':
                cursorX = Math.Min(buffer.width - 1, cursorX + first(1));
                break;
            case 'D':
                cursorX = Math.Max(0, cursorX - first(1));
                break;
            case 'm':
                applyGraphicRendition(values);
                break;
        }
    }

    private void applyGraphicRendition(int[] values)
    {
        if (values.Length == 1 && values[0] < 0)
        {
            pen = Style.terminalDefault; // "CSI m" is a reset
            return;
        }

        for (int i = 0; i < values.Length; i++)
        {
            int code = Math.Max(values[i], 0);
            switch (code)
            {
                case 0: pen = Style.terminalDefault; break;
                case 1: setAttribute(TextAttributes.Bold, true); break;
                case 2: setAttribute(TextAttributes.Dim, true); break;
                case 3: setAttribute(TextAttributes.Italic, true); break;
                case 4: setAttribute(TextAttributes.Underline, true); break;
                case 5: setAttribute(TextAttributes.Blink, true); break;
                case 7: setAttribute(TextAttributes.Reverse, true); break;
                case 9: setAttribute(TextAttributes.Strikethrough, true); break;
                case 22: setAttribute(TextAttributes.Bold | TextAttributes.Dim, false); break;
                case 23: setAttribute(TextAttributes.Italic, false); break;
                case 24: setAttribute(TextAttributes.Underline, false); break;
                case 25: setAttribute(TextAttributes.Blink, false); break;
                case 27: setAttribute(TextAttributes.Reverse, false); break;
                case 29: setAttribute(TextAttributes.Strikethrough, false); break;
                case 39: pen = pen.withForeground(Color.terminalDefault); break;
                case 49: pen = pen.withBackground(Color.terminalDefault); break;
                case >= 30 and <= 37: pen = pen.withForeground(palette(code - 30)); break;
                case >= 40 and <= 47: pen = pen.withBackground(palette(code - 40)); break;
                case >= 90 and <= 97: pen = pen.withForeground(palette(code - 90 + 8)); break;
                case >= 100 and <= 107: pen = pen.withBackground(palette(code - 100 + 8)); break;
                case 38:
                case 48:
                    i = applyExtendedColor(values, i, isBackground: code == 48);
                    break;
            }
        }
    }

    private int applyExtendedColor(int[] values, int index, bool isBackground)
    {
        if (index + 1 >= values.Length)
        {
            return values.Length;
        }

        Color? color = null;
        int consumed = index;
        if (values[index + 1] == 2 && index + 4 < values.Length)
        {
            color = Color.rgb((byte)values[index + 2], (byte)values[index + 3], (byte)values[index + 4]);
            consumed = index + 4;
        }
        else if (values[index + 1] == 5 && index + 2 < values.Length)
        {
            color = ColorQuantizer.fromAnsi256(values[index + 2]);
            consumed = index + 2;
        }

        if (color is not null)
        {
            pen = isBackground ? pen.withBackground(color.Value) : pen.withForeground(color.Value);
        }

        return consumed;
    }

    private void setAttribute(TextAttributes flag, bool on) =>
        pen = pen with { attributes = on ? pen.attributes | flag : pen.attributes & ~flag };

    private static Color palette(int index)
    {
        (byte r, byte g, byte b) = ColorQuantizer.ansi16Palette[index];
        return Color.rgb(r, g, b);
    }

    private void writeText(string text)
    {
        foreach (string grapheme in UnicodeWidth.graphemes(text))
        {
            int width = UnicodeWidth.ofGrapheme(grapheme);
            if (width == 0)
            {
                continue;
            }

            // Auto-wrap is disabled by the backend, so text that runs off the right edge is discarded.
            if (buffer.put(cursorX, cursorY, grapheme, width, pen))
            {
                cursorX += width;
            }
        }
    }
}
