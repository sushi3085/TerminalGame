using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Widgets;

/// <summary>
/// The JRPG message window: a framed box that types text out glyph by glyph, pauses after punctuation,
/// splits long text into pages, and waits for Confirm (first press completes the page, the next turns it).
/// Raises <see cref="completed"/> when the last page is dismissed.
/// </summary>
public sealed class DialogueBox : Widget
{
    private const string pausePunctuation = "，。、！？；：…,.!?;:";

    private sealed record Page(StyledLine[] lines, StyledGlyph[] flat);

    private StyledText fullText = StyledText.empty;
    private List<Page> pages = new();
    private int pageIndex;
    private int typedGlyphs;
    private double typingCarry;
    private double blinkClock;
    private int laidOutWidth = -1;
    private int laidOutHeight = -1;
    private bool needsPagination = true;

    public DialogueBox()
    {
        focusable = true;

        // A message window stretches to whatever it is given (full row in a column, remaining space in a row).
        layoutWidth = Length.fill();
    }

    /// <summary>Glyphs revealed per second. Zero or less shows pages instantly.</summary>
    public double charsPerSecond { get; set; } = 30;

    /// <summary>Extra delay (seconds) after . , ! ? 。 ， ！ ？ …</summary>
    public double punctuationPause { get; set; } = 0.18;

    /// <summary>Text rows requested from the parent (used by <see cref="measureContent"/>).</summary>
    public int preferredLines { get; set; } = 3;

    public Thickness padding { get; set; } = new(1, 0, 1, 0);

    public StyledText? speaker { get; private set; }

    public bool isTyping => pages.Count > 0 && typedGlyphs < pages[pageIndex].flat.Length;

    public bool isOnLastPage => pageIndex >= pages.Count - 1;

    public int currentPage => pageIndex;

    public int pageCount => pages.Count;

    /// <summary>True once the last page has been dismissed; reset by <see cref="show"/>.</summary>
    public bool isFinished { get; private set; }

    public event Action? completed;

    public event Action<int>? pageChanged;

    /// <summary>Starts a new message. <paramref name="speakerName"/> (markup allowed) appears in the frame's title.</summary>
    public void show(string markup, string? speakerName = null) =>
        show(Markup.parse(markup), speakerName);

    public void show(StyledText text, string? speakerName = null)
    {
        fullText = text;
        speaker = speakerName is null ? null : Markup.parse(speakerName);
        pageIndex = 0;
        typedGlyphs = 0;
        typingCarry = 0;
        blinkClock = 0;
        isFinished = false;
        needsPagination = true;
        if (laidOutWidth > 0)
        {
            paginate(laidOutWidth, laidOutHeight);
        }
    }

    /// <summary>Reveals the rest of the current page immediately.</summary>
    public void skipTyping()
    {
        if (pages.Count > 0)
        {
            typedGlyphs = pages[pageIndex].flat.Length;
        }
    }

    /// <summary>The Confirm behavior: finish typing, else next page, else complete. Returns whether anything happened.</summary>
    public bool advance()
    {
        if (pages.Count == 0 || isFinished)
        {
            return false;
        }

        if (isTyping)
        {
            skipTyping();
            return true;
        }

        if (!isOnLastPage)
        {
            pageIndex++;
            typedGlyphs = 0;
            typingCarry = 0;
            pageChanged?.Invoke(pageIndex);
            return true;
        }

        isFinished = true;
        completed?.Invoke();
        return true;
    }

    protected override Size measureContent(Size available)
    {
        int height = preferredLines + 2 + padding.vertical;
        return new Size(available.width, Math.Min(height, available.height));
    }

    public override void arrange(Rect rect)
    {
        base.arrange(rect);
        int innerWidth = Math.Max(0, rect.width - 2 - padding.horizontal);
        int innerHeight = Math.Max(0, rect.height - 2 - padding.vertical);
        if (needsPagination || innerWidth != laidOutWidth || innerHeight != laidOutHeight)
        {
            paginate(innerWidth, innerHeight);
        }
    }

    public override void update(double deltaSeconds)
    {
        blinkClock += deltaSeconds;
        if (pages.Count == 0 || !isTyping)
        {
            return;
        }

        StyledGlyph[] flat = pages[pageIndex].flat;
        if (charsPerSecond <= 0)
        {
            typedGlyphs = flat.Length;
            return;
        }

        double perGlyph = 1.0 / charsPerSecond;
        typingCarry += deltaSeconds;
        while (typedGlyphs < flat.Length)
        {
            double cost = perGlyph;
            if (typedGlyphs > 0 && pausePunctuation.Contains(flat[typedGlyphs - 1].text, StringComparison.Ordinal))
            {
                cost += punctuationPause;
            }

            if (typingCarry < cost)
            {
                break;
            }

            typingCarry -= cost;
            typedGlyphs++;
        }
    }

    public override bool handleInput(KeyEvent keyEvent) =>
        keyEvent.action == GameAction.Confirm && advance();

    public override void render(Canvas canvas)
    {
        Theme theme = effectiveTheme;
        Rect all = new(0, 0, canvas.size.width, canvas.size.height);
        canvas.fill(all, theme.panel);
        canvas.drawBorder(all, BorderStyle.rounded, theme.border);

        if (speaker is not null && canvas.size.width > 6)
        {
            StyledGlyph[] name = TextLayout.truncate(speaker.glyphs, canvas.size.width - 6);
            canvas.drawText(2, 0, " ", theme.title);
            int written = canvas.drawGlyphs(3, 0, name, theme.title);
            canvas.drawText(3 + written, 0, " ", theme.title);
        }

        if (pages.Count == 0)
        {
            return;
        }

        int left = 1 + padding.left;
        int top = 1 + padding.top;
        int quota = typedGlyphs;
        StyledLine[] lines = pages[pageIndex].lines;
        for (int row = 0; row < lines.Length && quota > 0; row++)
        {
            StyledGlyph[] glyphs = lines[row].glyphs;
            int visibleCount = Math.Min(quota, glyphs.Length);
            canvas.drawGlyphs(left, top + row, glyphs.AsSpan(0, visibleCount), theme.panel);
            quota -= visibleCount;
        }

        if (!isTyping && canvas.size.width > 4 && canvas.size.height > 2 && (int)(blinkClock * 2) % 2 == 0)
        {
            canvas.putGlyph(canvas.size.width - 3, canvas.size.height - 1, isOnLastPage ? "◆" : "▼", 1, theme.title);
        }
    }

    private void paginate(int innerWidth, int innerHeight)
    {
        laidOutWidth = innerWidth;
        laidOutHeight = innerHeight;
        needsPagination = false;

        IReadOnlyList<StyledLine> lines = TextLayout.wrap(fullText, innerWidth);
        int rowsPerPage = Math.Max(1, innerHeight);
        List<Page> built = new();
        for (int start = 0; start < lines.Count; start += rowsPerPage)
        {
            StyledLine[] chunk = lines.Skip(start).Take(rowsPerPage).ToArray();
            built.Add(new Page(chunk, chunk.SelectMany(l => l.glyphs).ToArray()));
        }

        if (built.Count == 0 && lines.Count == 0 && fullText.count > 0)
        {
            built.Add(new Page(Array.Empty<StyledLine>(), Array.Empty<StyledGlyph>()));
        }

        pages = built;
        pageIndex = Math.Min(pageIndex, Math.Max(0, pages.Count - 1));
        if (pages.Count > 0)
        {
            typedGlyphs = Math.Min(typedGlyphs, pages[pageIndex].flat.Length);
        }
    }
}
