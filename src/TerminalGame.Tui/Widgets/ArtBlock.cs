using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Widgets;

/// <summary>Static multi-line art (ASCII/Unicode sprites, logos, portraits) aligned inside its bounds.</summary>
public sealed class ArtBlock : Widget
{
    private StyledLine[] lines = Array.Empty<StyledLine>();

    public ArtBlock(string markup = "")
    {
        setArt(markup);
    }

    public HorizontalAlignment horizontalAlignment { get; set; } = HorizontalAlignment.Center;

    public VerticalAlignment verticalAlignment { get; set; } = VerticalAlignment.Center;

    public Style style { get; set; }

    /// <summary>Replaces the art. Each line is trimmed of trailing spaces only; markup colors are honored.</summary>
    public void setArt(string markup)
    {
        StyledText parsed = Markup.parse(markup);
        List<StyledLine> built = new();
        List<StyledGlyph> current = new();
        foreach (StyledGlyph glyph in parsed.glyphs)
        {
            if (glyph.isNewline)
            {
                built.Add(new StyledLine(current.ToArray()));
                current.Clear();
            }
            else
            {
                current.Add(glyph);
            }
        }

        if (current.Count > 0)
        {
            built.Add(new StyledLine(current.ToArray()));
        }

        lines = built.ToArray();
    }

    protected override Size measureContent(Size available) =>
        new(lines.Length == 0 ? 0 : lines.Max(l => l.width), lines.Length);

    public override void render(Canvas canvas)
    {
        int artWidth = lines.Length == 0 ? 0 : lines.Max(l => l.width);
        int top = verticalAlignment switch
        {
            VerticalAlignment.Center => (canvas.size.height - lines.Length) / 2,
            VerticalAlignment.Bottom => canvas.size.height - lines.Length,
            _ => 0,
        };

        int left = horizontalAlignment switch
        {
            HorizontalAlignment.Center => (canvas.size.width - artWidth) / 2,
            HorizontalAlignment.Right => canvas.size.width - artWidth,
            _ => 0,
        };

        Style baseStyle = effectiveTheme.text.overlay(style);
        for (int row = 0; row < lines.Length; row++)
        {
            canvas.drawLine(left, top + row, lines[row], baseStyle);
        }
    }
}
