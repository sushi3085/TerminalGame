using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Rendering;

/// <summary>
/// A drawing surface: a translated, clipped window onto a <see cref="ScreenBuffer"/>.
/// Coordinates are local — (0, 0) is the top-left of this canvas — and anything outside <see cref="size"/>
/// or outside an ancestor's clip is silently discarded, so widgets never need to bounds-check.
/// </summary>
public sealed class Canvas
{
    private readonly ScreenBuffer buffer;
    private readonly Point origin;
    private readonly Rect clip; // in buffer coordinates

    public Canvas(ScreenBuffer buffer)
        : this(buffer, new Point(0, 0), new Rect(0, 0, buffer.width, buffer.height), new Size(buffer.width, buffer.height))
    {
    }

    private Canvas(ScreenBuffer buffer, Point origin, Rect clip, Size size)
    {
        this.buffer = buffer;
        this.origin = origin;
        this.clip = clip;
        this.size = size;
    }

    public Size size { get; }

    /// <summary>A child canvas covering <paramref name="local"/> (in this canvas' coordinates).</summary>
    public Canvas sub(Rect local)
    {
        Rect absolute = new(origin.x + local.x, origin.y + local.y, Math.Max(0, local.width), Math.Max(0, local.height));
        return new Canvas(buffer, new Point(absolute.x, absolute.y), clip.intersect(absolute), new Size(absolute.width, absolute.height));
    }

    /// <summary>Fills a rectangle with a single-column glyph (a space by default).</summary>
    public void fill(Rect local, Style style, string glyph = " ")
    {
        Rect target = local.intersect(new Rect(0, 0, size.width, size.height));
        for (int y = target.y; y < target.bottom; y++)
        {
            for (int x = target.x; x < target.right; x++)
            {
                putGlyph(x, y, glyph, 1, style);
            }
        }
    }

    public void clear(Style style) => fill(new Rect(0, 0, size.width, size.height), style);

    /// <summary>Draws plain text on one line; returns the columns advanced.</summary>
    public int drawText(int x, int y, string text, Style style = default)
    {
        int cursor = x;
        foreach (string grapheme in UnicodeWidth.graphemes(text))
        {
            int width = UnicodeWidth.ofGrapheme(grapheme);
            if (width == 0)
            {
                continue;
            }

            putGlyph(cursor, y, grapheme, width, style);
            cursor += width;
        }

        return cursor - x;
    }

    /// <summary>Draws styled glyphs on one line over <paramref name="baseStyle"/>; returns the columns advanced.</summary>
    public int drawGlyphs(int x, int y, ReadOnlySpan<StyledGlyph> glyphs, Style baseStyle = default)
    {
        int cursor = x;
        foreach (StyledGlyph glyph in glyphs)
        {
            putGlyph(cursor, y, glyph.text, glyph.width, baseStyle.overlay(glyph.style));
            cursor += glyph.width;
        }

        return cursor - x;
    }

    public int drawLine(int x, int y, StyledLine line, Style baseStyle = default) =>
        drawGlyphs(x, y, line.glyphs, baseStyle);

    public void drawBorder(Rect local, BorderStyle border, Style style)
    {
        if (local.width < 2 || local.height < 2)
        {
            return;
        }

        int right = local.right - 1;
        int bottom = local.bottom - 1;
        for (int x = local.x + 1; x < right; x++)
        {
            putGlyph(x, local.y, border.top, 1, style);
            putGlyph(x, bottom, border.bottom, 1, style);
        }

        for (int y = local.y + 1; y < bottom; y++)
        {
            putGlyph(local.x, y, border.left, 1, style);
            putGlyph(right, y, border.right, 1, style);
        }

        putGlyph(local.x, local.y, border.topLeft, 1, style);
        putGlyph(right, local.y, border.topRight, 1, style);
        putGlyph(local.x, bottom, border.bottomLeft, 1, style);
        putGlyph(right, bottom, border.bottomRight, 1, style);
    }

    /// <summary>Writes one glyph, honoring the clip. A wide glyph that is only half visible becomes a blank.</summary>
    public void putGlyph(int x, int y, string glyph, int width, Style style)
    {
        int gx = origin.x + x;
        int gy = origin.y + y;
        if (width <= 0 || gy < clip.y || gy >= clip.bottom)
        {
            return;
        }

        if (width == 1)
        {
            if (gx >= clip.x && gx < clip.right)
            {
                buffer.put(gx, gy, glyph, 1, style);
            }

            return;
        }

        bool leftVisible = gx >= clip.x && gx < clip.right;
        bool rightVisible = gx + 1 >= clip.x && gx + 1 < clip.right;
        if (leftVisible && rightVisible)
        {
            buffer.put(gx, gy, glyph, 2, style);
        }
        else if (leftVisible)
        {
            buffer.put(gx, gy, " ", 1, style);
        }
        else if (rightVisible)
        {
            buffer.put(gx + 1, gy, " ", 1, style);
        }
    }
}
