using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Widgets;

/// <summary>One line of (marked-up) text. Longer text is cut with an ellipsis.</summary>
public sealed class Label : Widget
{
    private StyledText content = StyledText.empty;
    private StyledGlyph[] firstLine = Array.Empty<StyledGlyph>();

    public Label(string markup = "", HorizontalAlignment alignment = HorizontalAlignment.Left)
    {
        setText(markup);
        this.alignment = alignment;
    }

    public HorizontalAlignment alignment { get; set; }

    /// <summary>Layered over the theme's text style (transparent parts fall through).</summary>
    public Style style { get; set; }

    public StyledText text
    {
        get => content;
        set
        {
            content = value;
            firstLine = value.glyphs.TakeWhile(g => !g.isNewline).ToArray();
        }
    }

    public void setText(string markup) => text = Markup.parse(markup);

    protected override Size measureContent(Size available) => new(TextLayout.widthOf(firstLine), 1);

    public override void render(Canvas canvas)
    {
        if (canvas.size.height < 1)
        {
            return;
        }

        StyledGlyph[] fitted = TextLayout.truncate(firstLine, canvas.size.width);
        int slack = canvas.size.width - TextLayout.widthOf(fitted);
        int x = alignment switch
        {
            HorizontalAlignment.Center => slack / 2,
            HorizontalAlignment.Right => slack,
            _ => 0,
        };

        canvas.drawGlyphs(x, 0, fitted, effectiveTheme.text.overlay(style));
    }
}
