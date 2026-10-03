using System.Text;

namespace TerminalGame.Tui.Text;

/// <summary>One grapheme cluster together with its measured width and style.</summary>
public readonly record struct StyledGlyph(string text, int width, Style style)
{
    public bool isNewline => text == "\n";
    public bool isSpace => text == " ";
}

/// <summary>A single laid-out line of glyphs.</summary>
public sealed class StyledLine
{
    public StyledLine(StyledGlyph[] glyphs)
    {
        this.glyphs = glyphs;
        int total = 0;
        foreach (StyledGlyph glyph in glyphs)
        {
            total += glyph.width;
        }

        width = total;
    }

    public StyledGlyph[] glyphs { get; }
    public int width { get; }
}

/// <summary>
/// An immutable sequence of styled grapheme clusters. All text drawing and wrapping in the framework goes
/// through this type so that per-character colors (item names, damage numbers, …) survive line wrapping and
/// typewriter reveals.
/// </summary>
public sealed class StyledText
{
    public static readonly StyledText empty = new(Array.Empty<StyledGlyph>());

    private const int tabWidth = 4;

    public StyledText(IReadOnlyList<StyledGlyph> glyphs)
    {
        this.glyphs = glyphs;
    }

    public IReadOnlyList<StyledGlyph> glyphs { get; }

    public int count => glyphs.Count;

    /// <summary>Width of the widest line (explicit newlines only; no wrapping).</summary>
    public int maxLineWidth
    {
        get
        {
            int best = 0;
            int current = 0;
            foreach (StyledGlyph glyph in glyphs)
            {
                if (glyph.isNewline)
                {
                    best = Math.Max(best, current);
                    current = 0;
                }
                else
                {
                    current += glyph.width;
                }
            }

            return Math.Max(best, current);
        }
    }

    public static StyledText plain(string text, Style style = default)
    {
        Builder builder = new();
        builder.append(text, style);
        return builder.build();
    }

    public static StyledText fromMarkup(string markup, Style baseStyle = default) => Markup.parse(markup, baseStyle);

    public string toPlainString()
    {
        StringBuilder builder = new();
        foreach (StyledGlyph glyph in glyphs)
        {
            builder.Append(glyph.text);
        }

        return builder.ToString();
    }

    /// <summary>Accumulates text runs, normalizing line endings and tabs and dropping control characters.</summary>
    public sealed class Builder
    {
        private readonly List<StyledGlyph> items = new();

        public Builder append(string text, Style style)
        {
            text = text.Replace("\r\n", "\n");
            foreach (string grapheme in UnicodeWidth.graphemes(text))
            {
                if (grapheme == "\n")
                {
                    items.Add(new StyledGlyph("\n", 0, style));
                }
                else if (grapheme == "\t")
                {
                    for (int i = 0; i < tabWidth; i++)
                    {
                        items.Add(new StyledGlyph(" ", 1, style));
                    }
                }
                else
                {
                    int width = UnicodeWidth.ofGrapheme(grapheme);
                    if (width > 0)
                    {
                        items.Add(new StyledGlyph(grapheme, width, style));
                    }
                }
            }

            return this;
        }

        public StyledText build() => new(items.ToArray());
    }
}
