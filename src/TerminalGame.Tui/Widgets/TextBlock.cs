using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Widgets;

/// <summary>Word-wrapped, CJK-aware multi-line text with optional vertical scrolling.</summary>
public sealed class TextBlock : Widget
{
    private StyledText content = StyledText.empty;
    private IReadOnlyList<StyledLine> cachedLines = Array.Empty<StyledLine>();
    private int cachedWidth = -1;

    public TextBlock(string markup = "")
    {
        setText(markup);
    }

    public HorizontalAlignment alignment { get; set; }

    public Style style { get; set; }

    /// <summary>First visible line; clamped (and stored back) while rendering, so callers can simply add or subtract 1.</summary>
    public int scrollOffset { get; set; }

    public StyledText text
    {
        get => content;
        set
        {
            content = value;
            cachedWidth = -1;
        }
    }

    public void setText(string markup) => text = Markup.parse(markup);

    /// <summary>Total wrapped line count at the last layout width (for scroll indicators).</summary>
    public int lineCount => cachedLines.Count;

    protected override Size measureContent(Size available)
    {
        IReadOnlyList<StyledLine> lines = linesFor(available.width);
        int widest = 0;
        foreach (StyledLine line in lines)
        {
            widest = Math.Max(widest, line.width);
        }

        return new Size(widest, lines.Count);
    }

    public override void render(Canvas canvas)
    {
        IReadOnlyList<StyledLine> lines = linesFor(canvas.size.width);
        int maxOffset = Math.Max(0, lines.Count - canvas.size.height);
        int first = Math.Clamp(scrollOffset, 0, maxOffset);
        scrollOffset = first;
        Style baseStyle = effectiveTheme.text.overlay(style);

        for (int row = 0; row < canvas.size.height && first + row < lines.Count; row++)
        {
            StyledLine line = lines[first + row];
            int slack = canvas.size.width - line.width;
            int x = alignment switch
            {
                HorizontalAlignment.Center => slack / 2,
                HorizontalAlignment.Right => slack,
                _ => 0,
            };

            canvas.drawLine(x, row, line, baseStyle);
        }
    }

    private IReadOnlyList<StyledLine> linesFor(int width)
    {
        if (width != cachedWidth)
        {
            cachedLines = TextLayout.wrap(content, width);
            cachedWidth = width;
        }

        return cachedLines;
    }
}
