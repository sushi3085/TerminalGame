namespace TerminalGame.Tui.Text;

/// <summary>
/// Line breaking for mixed Latin / CJK text.
/// <list type="bullet">
/// <item>Latin: break after spaces (a space that overflows the line "hangs" and is dropped).</item>
/// <item>CJK: break between any two glyphs where either is double-width, except that closing punctuation
/// (。，」…) may not start a line and opening brackets (「（) may not end one — a minimal kinsoku shori.</item>
/// <item>A token longer than the line is broken hard.</item>
/// </list>
/// </summary>
public static class TextLayout
{
    // Characters that must not begin a line.
    private const string noLineStart = "，。、．：；！？）」』】〉》〕］｝…‥ー々ゝゞ,.;:!?)]}%”’";

    // Characters that must not end a line.
    private const string noLineEnd = "（「『【〈《〔［｛([{“‘";

    public static IReadOnlyList<StyledLine> wrap(StyledText text, int maxWidth)
    {
        List<StyledLine> lines = new();
        if (maxWidth <= 0)
        {
            return lines;
        }

        List<StyledGlyph> current = new();
        int currentWidth = 0;
        bool softWrapped = false;

        foreach (StyledGlyph glyph in text.glyphs)
        {
            if (glyph.isNewline)
            {
                lines.Add(makeLine(current));
                current = new List<StyledGlyph>();
                currentWidth = 0;
                softWrapped = false;
                continue;
            }

            if (glyph.isSpace && currentWidth + glyph.width > maxWidth)
            {
                // The line is exactly full: the space hangs off the end and is dropped.
                lines.Add(makeLine(trimTrailingSpaces(current)));
                current = new List<StyledGlyph>();
                currentWidth = 0;
                softWrapped = true;
                continue;
            }

            while (current.Count > 0 && currentWidth + glyph.width > maxWidth)
            {
                int split = findBreak(current, glyph);
                if (split <= 0)
                {
                    split = current.Count;
                }

                List<StyledGlyph> head = current.GetRange(0, split);
                List<StyledGlyph> tail = current.GetRange(split, current.Count - split);
                lines.Add(makeLine(trimTrailingSpaces(head)));
                current = tail;
                currentWidth = widthOf(tail);
                softWrapped = true;
            }

            if (glyph.isSpace && current.Count == 0 && softWrapped)
            {
                continue; // no leading spaces on a wrapped continuation line
            }

            current.Add(glyph);
            currentWidth += glyph.width;
            if (!glyph.isSpace)
            {
                softWrapped = false;
            }
        }

        if (current.Count > 0)
        {
            lines.Add(makeLine(current));
        }

        return lines;
    }

    /// <summary>Returns <paramref name="glyphs"/> clipped to <paramref name="maxWidth"/> columns, with an ellipsis if cut.</summary>
    public static StyledGlyph[] truncate(IReadOnlyList<StyledGlyph> glyphs, int maxWidth, string ellipsis = "…")
    {
        if (maxWidth <= 0)
        {
            return Array.Empty<StyledGlyph>();
        }

        if (widthOf(glyphs) <= maxWidth)
        {
            return glyphs.ToArray();
        }

        int ellipsisWidth = UnicodeWidth.ofString(ellipsis);
        int budget = maxWidth - ellipsisWidth;
        List<StyledGlyph> result = new();
        int used = 0;
        foreach (StyledGlyph glyph in glyphs)
        {
            if (used + glyph.width > budget)
            {
                break;
            }

            result.Add(glyph);
            used += glyph.width;
        }

        Style style = result.Count > 0 ? result[^1].style : default;
        result.Add(new StyledGlyph(ellipsis, ellipsisWidth, style));
        return result.ToArray();
    }

    public static int widthOf(IReadOnlyList<StyledGlyph> glyphs)
    {
        int total = 0;
        foreach (StyledGlyph glyph in glyphs)
        {
            total += glyph.width;
        }

        return total;
    }

    private static int findBreak(List<StyledGlyph> current, StyledGlyph incoming)
    {
        for (int split = current.Count; split >= 1; split--)
        {
            StyledGlyph next = split == current.Count ? incoming : current[split];
            if (canBreakBetween(current[split - 1], next))
            {
                return split;
            }
        }

        return -1;
    }

    private static bool canBreakBetween(StyledGlyph previous, StyledGlyph next)
    {
        if (previous.isSpace)
        {
            return true;
        }

        if (next.isSpace || noLineStart.Contains(next.text, StringComparison.Ordinal))
        {
            return false;
        }

        if (noLineEnd.Contains(previous.text, StringComparison.Ordinal))
        {
            return false;
        }

        return previous.width == 2 || next.width == 2 || previous.text == "-";
    }

    private static List<StyledGlyph> trimTrailingSpaces(List<StyledGlyph> glyphs)
    {
        int end = glyphs.Count;
        while (end > 0 && glyphs[end - 1].isSpace)
        {
            end--;
        }

        return end == glyphs.Count ? glyphs : glyphs.GetRange(0, end);
    }

    private static StyledLine makeLine(List<StyledGlyph> glyphs) => new(glyphs.ToArray());
}
