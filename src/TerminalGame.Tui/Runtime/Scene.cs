using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Tui.Runtime;

/// <summary>
/// One screen of the game (title, field, battle, pause menu…). A scene owns a widget tree, keyboard focus
/// and a stack of modal widgets (message boxes, choice popups) drawn above the tree. Scenes are managed by
/// <see cref="Application"/> as a stack; only the top scene receives input and updates.
/// </summary>
public abstract class Scene
{
    private sealed record ModalEntry(Widget widget, HorizontalAlignment horizontal, VerticalAlignment vertical, Widget? previousFocus);

    private readonly List<ModalEntry> modals = new();

    /// <summary>Set while the scene is on the application's stack.</summary>
    public Application? application { get; internal set; }

    /// <summary>Root of the widget tree; it is arranged to fill the whole screen.</summary>
    public Widget? root { get; set; }

    /// <summary>If true, the scene below is still drawn underneath (pause menus, popups). It still gets no input.</summary>
    public virtual bool isTransparent => false;

    public Widget? focusedWidget { get; private set; }

    public bool hasModal => modals.Count > 0;

    public virtual void onEnter()
    {
    }

    public virtual void onExit()
    {
    }

    /// <summary>Another scene was pushed on top.</summary>
    public virtual void onPause()
    {
    }

    /// <summary>The scene above was popped.</summary>
    public virtual void onResume()
    {
    }

    public virtual void onUpdate(double deltaSeconds)
    {
    }

    /// <summary>Key presses nobody in the focus chain consumed.</summary>
    public virtual void onKey(KeyEvent keyEvent)
    {
    }

    public virtual void onResize(Size newSize)
    {
    }

    public void setFocus(Widget? widget)
    {
        if (ReferenceEquals(widget, focusedWidget))
        {
            return;
        }

        Widget? old = focusedWidget;
        focusedWidget = widget;
        if (old is not null)
        {
            old.focused = false;
            old.onFocusChanged();
        }

        if (widget is not null)
        {
            widget.focused = true;
            widget.onFocusChanged();
        }
    }

    /// <summary>
    /// Shows <paramref name="widget"/> above the scene and gives it focus. While a modal is open, input never
    /// reaches the scene below it. Modals stack; <see cref="closeModal"/> restores the previous focus.
    /// </summary>
    public void showModal(Widget widget, HorizontalAlignment horizontal = HorizontalAlignment.Center, VerticalAlignment vertical = VerticalAlignment.Center)
    {
        widget.theme ??= root?.effectiveTheme;
        modals.Add(new ModalEntry(widget, horizontal, vertical, focusedWidget));
        setFocus(widget.findFocusable() ?? widget);
    }

    public void closeModal(Widget widget)
    {
        int index = modals.FindIndex(m => ReferenceEquals(m.widget, widget));
        if (index < 0)
        {
            return;
        }

        ModalEntry entry = modals[index];
        modals.RemoveAt(index);
        if (index == modals.Count)
        {
            // The topmost modal closed: hand focus back to whatever had it before.
            setFocus(entry.previousFocus);
        }
    }

    internal void tick(double deltaSeconds)
    {
        root?.update(deltaSeconds);
        foreach (ModalEntry entry in modals.ToArray())
        {
            entry.widget.update(deltaSeconds);
        }

        onUpdate(deltaSeconds);
    }

    internal void dispatchKey(KeyEvent keyEvent)
    {
        for (Widget? target = focusedWidget; target is not null; target = target.parent)
        {
            if (target.handleInput(keyEvent))
            {
                return;
            }
        }

        if (modals.Count == 0)
        {
            onKey(keyEvent);
        }
    }

    internal void renderTo(Canvas canvas)
    {
        Size size = canvas.size;
        if (root is not null)
        {
            root.measure(size);
            root.arrange(new Rect(0, 0, size.width, size.height));
            root.render(canvas.sub(root.bounds));
        }

        foreach (ModalEntry entry in modals)
        {
            Size desired = entry.widget.measure(size).clampTo(size);
            int x = entry.horizontal switch
            {
                HorizontalAlignment.Left => 0,
                HorizontalAlignment.Right => size.width - desired.width,
                _ => (size.width - desired.width) / 2,
            };

            int y = entry.vertical switch
            {
                VerticalAlignment.Top => 0,
                VerticalAlignment.Bottom => size.height - desired.height,
                _ => (size.height - desired.height) / 2,
            };

            entry.widget.arrange(new Rect(x, y, desired.width, desired.height));
            entry.widget.render(canvas.sub(entry.widget.bounds));
        }
    }
}
