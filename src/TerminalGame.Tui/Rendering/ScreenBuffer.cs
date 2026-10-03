namespace TerminalGame.Tui.Rendering;

/// <summary>
/// A width x height grid of <see cref="Cell"/>s. All writes keep the double-width invariant:
/// a wide glyph is always followed by exactly one continuation cell, and no continuation cell is ever orphaned.
/// </summary>
public sealed class ScreenBuffer
{
    private Cell[] cells;

    public ScreenBuffer(int width, int height)
    {
        this.width = Math.Max(0, width);
        this.height = Math.Max(0, height);
        cells = new Cell[this.width * this.height];
        clear(Style.terminalDefault);
    }

    public int width { get; private set; }
    public int height { get; private set; }

    public Cell get(int x, int y) => cells[y * width + x];

    public bool inBounds(int x, int y) => x >= 0 && y >= 0 && x < width && y < height;

    /// <summary>Resets every cell to a blank with the given (resolved) style.</summary>
    public void clear(Style style)
    {
        Cell blank = Cell.blank(resolve(style, Style.terminalDefault));
        Array.Fill(cells, blank);
    }

    public void resize(int newWidth, int newHeight)
    {
        width = Math.Max(0, newWidth);
        height = Math.Max(0, newHeight);
        cells = new Cell[width * height];
        clear(Style.terminalDefault);
    }

    /// <summary>
    /// Writes one grapheme cluster of the given display width (1 or 2) with its left edge at (x, y).
    /// Transparent style colors are resolved against the cell being overwritten (background) or the
    /// terminal default (foreground). Returns false, writing nothing, if the glyph does not fit in the buffer.
    /// </summary>
    public bool put(int x, int y, string glyph, int glyphWidth, Style style)
    {
        if (glyphWidth < 1 || glyphWidth > 2 || y < 0 || y >= height || x < 0 || x + glyphWidth > width)
        {
            return false;
        }

        int row = y * width;
        orphanCheck(row, x, glyphWidth);

        Style resolved = resolve(style, cells[row + x].style);
        cells[row + x] = new Cell(glyph, resolved, false);
        if (glyphWidth == 2)
        {
            cells[row + x + 1] = Cell.continuation(resolved);
        }

        return true;
    }

    /// <summary>
    /// Stores a cell verbatim, bypassing glyph-width bookkeeping. Only for mirrors of an already-valid buffer
    /// (the renderer's "what the terminal shows" copy).
    /// </summary>
    internal void setRaw(int x, int y, Cell cell) => cells[y * width + x] = cell;

    /// <summary>The glyphs of one row as a string (continuation cells skipped); handy for tests and debugging.</summary>
    public string rowToString(int y)
    {
        System.Text.StringBuilder builder = new();
        for (int x = 0; x < width; x++)
        {
            Cell cell = cells[y * width + x];
            if (!cell.isContinuation)
            {
                builder.Append(cell.glyph);
            }
        }

        return builder.ToString();
    }

    private void orphanCheck(int row, int x, int glyphWidth)
    {
        // Overwriting the right half of a wide glyph orphans its left half.
        if (cells[row + x].isContinuation && x > 0)
        {
            blankOut(row + x - 1);
        }

        // Overwriting the left half of a wide glyph (when our glyph is narrower) orphans its right half.
        int after = x + glyphWidth;
        if (after < width && cells[row + after].isContinuation)
        {
            blankOut(row + after);
        }
    }

    private void blankOut(int index)
    {
        Cell old = cells[index];
        cells[index] = Cell.blank(old.style with { attributes = TextAttributes.None });
    }

    private static Style resolve(Style style, Style underneath) => new(
        style.foreground.isTransparent ? Color.terminalDefault : style.foreground,
        style.background.isTransparent ? underneath.background : style.background,
        style.attributes);
}
