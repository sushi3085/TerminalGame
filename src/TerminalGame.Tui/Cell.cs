namespace TerminalGame.Tui;

/// <summary>
/// One terminal cell.
/// A double-width glyph occupies two cells: the left one holds the glyph, the right one is a
/// <see cref="isContinuation"/> placeholder carrying the same style. Stored styles are always resolved
/// (no transparent colors).
/// </summary>
public readonly record struct Cell(string glyph, Style style, bool isContinuation)
{
    /// <summary>A cell that can never equal a real one; used to force the renderer to repaint.</summary>
    public static readonly Cell unknown = new("\0", Style.terminalDefault, false);

    public static Cell blank(Style style) => new(" ", style, false);

    public static Cell continuation(Style style) => new(string.Empty, style, true);
}
