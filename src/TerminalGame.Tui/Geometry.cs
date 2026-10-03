namespace TerminalGame.Tui;

public readonly record struct Point(int x, int y);

public readonly record struct Size(int width, int height)
{
    public static readonly Size zero = new(0, 0);

    public Size clampTo(Size max) => new(Math.Min(width, max.width), Math.Min(height, max.height));
}

/// <summary>Space reserved on each side of a rectangle (padding / margin).</summary>
public readonly record struct Thickness(int left, int top, int right, int bottom)
{
    public static readonly Thickness none = default;

    public int horizontal => left + right;
    public int vertical => top + bottom;

    public static Thickness all(int amount) => new(amount, amount, amount, amount);

    public static Thickness symmetric(int horizontal, int vertical) => new(horizontal, vertical, horizontal, vertical);
}

public readonly record struct Rect(int x, int y, int width, int height)
{
    public static readonly Rect empty = default;

    public int right => x + width;
    public int bottom => y + height;
    public bool isEmpty => width <= 0 || height <= 0;
    public Point location => new(x, y);
    public Size size => new(width, height);

    public bool contains(int px, int py) => px >= x && px < right && py >= y && py < bottom;

    public Rect intersect(Rect other)
    {
        int left = Math.Max(x, other.x);
        int top = Math.Max(y, other.y);
        int rightEdge = Math.Min(right, other.right);
        int bottomEdge = Math.Min(bottom, other.bottom);
        return rightEdge <= left || bottomEdge <= top
            ? new Rect(left, top, 0, 0)
            : new Rect(left, top, rightEdge - left, bottomEdge - top);
    }

    public Rect offset(int dx, int dy) => new(x + dx, y + dy, width, height);

    /// <summary>Shrinks the rectangle by <paramref name="amount"/> on each side (never below zero size).</summary>
    public Rect deflate(Thickness amount) => new(
        x + amount.left,
        y + amount.top,
        Math.Max(0, width - amount.horizontal),
        Math.Max(0, height - amount.vertical));
}
