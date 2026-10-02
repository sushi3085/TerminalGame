using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;

namespace TerminalGame.Tui.Widgets;

/// <summary>A widget that owns children. Subclasses decide how they are laid out.</summary>
public abstract class Container : Widget
{
    private readonly List<Widget> childList = new();

    public IReadOnlyList<Widget> children => childList;

    public virtual void add(Widget child)
    {
        if (child.parent is not null)
        {
            throw new InvalidOperationException("The widget already has a parent.");
        }

        child.parent = this;
        childList.Add(child);
    }

    public virtual void remove(Widget child)
    {
        if (childList.Remove(child))
        {
            child.parent = null;
        }
    }

    public void clearChildren()
    {
        foreach (Widget child in childList)
        {
            child.parent = null;
        }

        childList.Clear();
    }

    public override void render(Canvas canvas)
    {
        foreach (Widget child in childList)
        {
            if (child.visible)
            {
                child.render(canvas.sub(child.bounds));
            }
        }
    }

    public override void update(double deltaSeconds)
    {
        foreach (Widget child in childList)
        {
            child.update(deltaSeconds);
        }
    }

    public override Widget? findFocusable()
    {
        if (focusable)
        {
            return this;
        }

        foreach (Widget child in childList)
        {
            if (child.visible && child.findFocusable() is Widget found)
            {
                return found;
            }
        }

        return null;
    }
}
