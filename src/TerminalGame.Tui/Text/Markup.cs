namespace TerminalGame.Tui.Text;

/// <summary>
/// Tiny inline markup for dialogue and menu text.
/// <code>
/// "你獲得了 [gold b]傳說之劍[/]！"      // [/] pops the most recent tag
/// "[red]HP[/] 12 / [#40c060]34[/]"      // named colors or #rrggbb / #rgb
/// "[on:blue white]反白[/]"               // on:&lt;color&gt; sets the background
/// "[[literal bracket]"                  // [[ is an escaped '['
/// </code>
/// Tags: <c>b i u dim rev strike blink</c>, any color name, <c>#hex</c>, <c>on:color</c>. Several can share one
/// bracket (<c>[b red]</c>). Anything that is not a valid tag — such as "[Potion]" — is kept as literal text,
/// so game strings do not need escaping.
/// </summary>
public static class Markup
{
    public static StyledText parse(string markup, Style baseStyle = default)
    {
        StyledText.Builder builder = new();
        Stack<Style> stack = new();
        Style current = baseStyle;
        int literalStart = 0;
        int i = 0;

        void flush(int end)
        {
            if (end > literalStart)
            {
                builder.append(markup.Substring(literalStart, end - literalStart), current);
            }
        }

        while (i < markup.Length)
        {
            if (markup[i] != '[')
            {
                i++;
                continue;
            }

            if (i + 1 < markup.Length && markup[i + 1] == '[')
            {
                // "[[" -> literal '['
                flush(i);
                builder.append("[", current);
                i += 2;
                literalStart = i;
                continue;
            }

            int close = markup.IndexOf(']', i + 1);
            if (close < 0)
            {
                break;
            }

            string tag = markup.Substring(i + 1, close - i - 1).Trim();
            if (tag == "/" && stack.Count > 0)
            {
                flush(i);
                current = stack.Pop();
                i = close + 1;
                literalStart = i;
            }
            else if (tag.Length > 0 && tryApplyTag(tag, current, out Style next))
            {
                flush(i);
                stack.Push(current);
                current = next;
                i = close + 1;
                literalStart = i;
            }
            else
            {
                i++;
            }
        }

        flush(markup.Length);
        return builder.build();
    }

    /// <summary>Removes all markup, leaving only the visible characters.</summary>
    public static string strip(string markup) => parse(markup).toPlainString();

    private static bool tryApplyTag(string tag, Style style, out Style result)
    {
        result = style;
        foreach (string token in tag.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!tryApplyToken(token, ref result))
            {
                return false;
            }
        }

        return true;
    }

    private static bool tryApplyToken(string token, ref Style style)
    {
        switch (token.ToLowerInvariant())
        {
            case "b":
            case "bold":
                style = style.withAttributes(TextAttributes.Bold);
                return true;
            case "i":
            case "italic":
                style = style.withAttributes(TextAttributes.Italic);
                return true;
            case "u":
            case "underline":
                style = style.withAttributes(TextAttributes.Underline);
                return true;
            case "dim":
                style = style.withAttributes(TextAttributes.Dim);
                return true;
            case "rev":
            case "reverse":
                style = style.withAttributes(TextAttributes.Reverse);
                return true;
            case "strike":
                style = style.withAttributes(TextAttributes.Strikethrough);
                return true;
            case "blink":
                style = style.withAttributes(TextAttributes.Blink);
                return true;
        }

        if (token.StartsWith("on:", StringComparison.OrdinalIgnoreCase))
        {
            if (Color.tryParse(token.Substring(3), out Color background))
            {
                style = style.withBackground(background);
                return true;
            }

            return false;
        }

        if (Color.tryParse(token, out Color foreground))
        {
            style = style.withForeground(foreground);
            return true;
        }

        return false;
    }
}
