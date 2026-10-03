using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;

namespace TerminalGame.Tui.Widgets;

public enum HorizontalAlignment
{
    Left,
    Center,
    Right,
}

public enum VerticalAlignment
{
    Top,
    Center,
    Bottom,
}

public enum LengthKind
{
    /// <summary>Use the widget's measured size.</summary>
    Auto,

    /// <summary>Exactly this many cells.</summary>
    Fixed,

    /// <summary>A share of whatever space is left, proportional to the weight.</summary>
    Fill,
}

/// <summary>How a widget wants to be sized along one axis. Containers interpret it.</summary>
public readonly record struct Length(LengthKind kind, int value)
{
    public static readonly Length auto = new(LengthKind.Auto, 0);

    public static Length cells(int count) => new(LengthKind.Fixed, Math.Max(0, count));

    public static Length fill(int weight = 1) => new(LengthKind.Fill, Math.Max(1, weight));
}

/// <summary>
/// Base class of everything on screen. Layout is two-pass, like WPF/Flutter:
/// <see cref="measure"/> asks "how big would you like to be within this much space?", then the parent calls
/// <see cref="arrange"/> with the final rectangle (in the parent's coordinates). <see cref="render"/> receives a
/// canvas already translated and clipped to the widget's bounds, so a widget always draws from (0, 0).
/// </summary>
public abstract class Widget
{
    private Theme? themeOverride;

    public Widget? parent { get; internal set; }

    /// <summary>Final rectangle in the parent's coordinate space; set by <see cref="arrange"/>.</summary>
    public Rect bounds { get; protected set; }

    public bool visible { get; set; } = true;

    /// <summary>Preferred width / height policy, read by the parent container.</summary>
    public Length layoutWidth { get; set; } = Length.auto;

    public Length layoutHeight { get; set; } = Length.auto;

    /// <summary>Whether this widget can take keyboard focus.</summary>
    public bool focusable { get; protected set; }

    public bool focused { get; internal set; }

    /// <summary>Theme override; when null the parent's theme (ultimately <see cref="Theme.defaultTheme"/>) applies.</summary>
    public Theme? theme
    {
        get => themeOverride;
        set => themeOverride = value;
    }

    public Theme effectiveTheme => themeOverride ?? parent?.effectiveTheme ?? Theme.defaultTheme;

    /// <summary>Desired size within <paramref name="available"/>, honoring a <see cref="LengthKind.Fixed"/> layout length.</summary>
    public Size measure(Size available)
    {
        Size desired = measureContent(available);
        int width = layoutWidth.kind == LengthKind.Fixed ? layoutWidth.value : desired.width;
        int height = layoutHeight.kind == LengthKind.Fixed ? layoutHeight.value : desired.height;
        return new Size(width, height);
    }

    protected virtual Size measureContent(Size available) => Size.zero;

    public virtual void arrange(Rect rect) => bounds = rect;

    public abstract void render(Canvas canvas);

    /// <summary>Advances animations; <paramref name="deltaSeconds"/> is the real time since the last frame.</summary>
    public virtual void update(double deltaSeconds)
    {
    }

    /// <summary>Handles a key press; return true if it was consumed (otherwise it bubbles to the parent).</summary>
    public virtual bool handleInput(KeyEvent keyEvent) => false;

    public virtual void onFocusChanged()
    {
    }

    /// <summary>This widget if focusable, otherwise the first focusable descendant (depth-first), otherwise null.</summary>
    public virtual Widget? findFocusable() => focusable ? this : null;
}
