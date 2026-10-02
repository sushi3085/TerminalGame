using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Widgets;

/// <summary>A framed panel around one child, with an optional title in the top edge.</summary>
public sealed class Border : Widget
{
    private Widget? childWidget;

    public Border(Widget? child = null, string? title = null)
    {
        this.child = child;
        if (title is not null)
        {
            titleText = Markup.parse(title);
        }
    }

    public Widget? child
    {
        get => childWidget;
        set
        {
            if (childWidget is not null)
            {
                childWidget.parent = null;
            }

            childWidget = value;
            if (value is not null)
            {
                if (value.parent is not null)
                {
                    throw new InvalidOperationException("The widget already has a parent.");
                }

                value.parent = this;
            }
        }
    }

    public StyledText? titleText { get; set; }

    public BorderStyle glyphs { get; set; } = BorderStyle.single;

    /// <summary>Space between the frame and the child.</summary>
    public Thickness padding { get; set; } = new(1, 0, 1, 0);

    /// <summary>Paint the panel background inside the frame (the theme's window color).</summary>
    public bool fillBackground { get; set; } = true;

    public void setTitle(string? markup) => titleText = markup is null ? null : Markup.parse(markup);

    protected override Size measureContent(Size available)
    {
        int chromeWidth = 2 + padding.horizontal;
        int chromeHeight = 2 + padding.vertical;
        Size inner = childWidget is null
            ? Size.zero
            : childWidget.measure(new Size(Math.Max(0, available.width - chromeWidth), Math.Max(0, available.height - chromeHeight)));
        int titleWidth = titleText is null ? 0 : titleText.maxLineWidth + 4;
        return new Size(Math.Max(inner.width + chromeWidth, titleWidth), inner.height + chromeHeight);
    }

    public override void arrange(Rect rect)
    {
        base.arrange(rect);
        childWidget?.arrange(contentRect(rect.width, rect.height));
    }

    public override void render(Canvas canvas)
    {
        Theme theme = effectiveTheme;
        Rect all = new(0, 0, canvas.size.width, canvas.size.height);
        if (fillBackground)
        {
            canvas.fill(all, theme.panel);
        }

        canvas.drawBorder(all, glyphs, theme.border);

        if (titleText is not null && canvas.size.width > 4)
        {
            StyledGlyph[] fitted = TextLayout.truncate(titleText.glyphs, canvas.size.width - 4);
            canvas.drawText(1, 0, " ", theme.title);
            int written = canvas.drawGlyphs(2, 0, fitted, theme.title);
            canvas.drawText(2 + written, 0, " ", theme.title);
        }

        if (childWidget is { visible: true })
        {
            childWidget.render(canvas.sub(childWidget.bounds));
        }
    }

    public override void update(double deltaSeconds) => childWidget?.update(deltaSeconds);

    public override Widget? findFocusable() => childWidget?.findFocusable();

    private Rect contentRect(int width, int height) => new(
        1 + padding.left,
        1 + padding.top,
        Math.Max(0, width - 2 - padding.horizontal),
        Math.Max(0, height - 2 - padding.vertical));
}
