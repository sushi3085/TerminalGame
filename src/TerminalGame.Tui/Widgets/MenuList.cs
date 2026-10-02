using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Widgets;

public sealed record MenuItem(StyledText label, bool enabled = true, object? tag = null)
{
    /// <summary>Builds an item from markup text, e.g. <c>MenuItem.of("[gold]傷藥[/] x3")</c>.</summary>
    public static MenuItem of(string markup, bool enabled = true, object? tag = null) =>
        new(Markup.parse(markup), enabled, tag);
}

/// <summary>
/// A vertical list with a cursor: the backbone of command menus, item lists and title screens.
/// Up/Down move (skipping disabled entries, optionally wrapping), Confirm / Cancel raise events.
/// Long lists scroll to keep the cursor visible.
/// </summary>
public sealed class MenuList : Widget
{
    private readonly List<MenuItem> items = new();
    private int scrollTop;
    private int visibleRows = int.MaxValue;

    public MenuList(IEnumerable<MenuItem>? items = null)
    {
        focusable = true;
        if (items is not null)
        {
            setItems(items);
        }
    }

    public IReadOnlyList<MenuItem> entries => items;

    public int selectedIndex { get; private set; }

    public MenuItem? selectedItem => items.Count == 0 ? null : items[selectedIndex];

    public bool wrapAround { get; set; } = true;

    /// <summary>Drawn in front of the selected entry; must be one column wide.</summary>
    public string cursorGlyph { get; set; } = "▶";

    /// <summary>Raised with the entry index when Confirm is pressed on an enabled entry.</summary>
    public event Action<int>? confirmed;

    public event Action? cancelled;

    public event Action<int>? selectionChanged;

    public void setItems(IEnumerable<MenuItem> newItems)
    {
        items.Clear();
        items.AddRange(newItems);
        selectedIndex = 0;
        scrollTop = 0;
        if (items.Count > 0 && !items[0].enabled)
        {
            moveSelection(1);
        }
    }

    public void select(int index)
    {
        if (items.Count == 0)
        {
            return;
        }

        int clamped = Math.Clamp(index, 0, items.Count - 1);
        if (clamped != selectedIndex)
        {
            selectedIndex = clamped;
            ensureVisible();
            selectionChanged?.Invoke(selectedIndex);
        }
    }

    protected override Size measureContent(Size available)
    {
        int widest = 0;
        foreach (MenuItem item in items)
        {
            widest = Math.Max(widest, item.label.maxLineWidth);
        }

        return new Size(widest + 2, items.Count);
    }

    public override void arrange(Rect rect)
    {
        base.arrange(rect);
        visibleRows = Math.Max(1, rect.height);
        ensureVisible();
    }

    public override bool handleInput(KeyEvent keyEvent)
    {
        switch (keyEvent.action)
        {
            case GameAction.Up:
                moveSelection(-1);
                return true;
            case GameAction.Down:
                moveSelection(1);
                return true;
            case GameAction.PageUp:
                moveSelection(-Math.Max(1, visibleRows - 1), allowWrap: false);
                return true;
            case GameAction.PageDown:
                moveSelection(Math.Max(1, visibleRows - 1), allowWrap: false);
                return true;
            case GameAction.Confirm when confirmed is not null:
                if (items.Count > 0 && items[selectedIndex].enabled)
                {
                    confirmed.Invoke(selectedIndex);
                }

                return true;
            case GameAction.Cancel when cancelled is not null:
                cancelled.Invoke();
                return true;
            default:
                return false;
        }
    }

    public override void render(Canvas canvas)
    {
        Theme theme = effectiveTheme;
        Style rowBase = theme.text;
        Style selectedStyle = focused ? theme.selection : theme.selectionInactive;

        for (int row = 0; row < canvas.size.height; row++)
        {
            int index = scrollTop + row;
            if (index >= items.Count)
            {
                break;
            }

            MenuItem item = items[index];
            bool isSelected = index == selectedIndex;
            Style style = item.enabled ? rowBase : theme.disabled;
            if (isSelected)
            {
                style = style.overlay(selectedStyle);
                canvas.fill(new Rect(0, row, canvas.size.width, 1), selectedStyle);
                canvas.drawText(0, row, cursorGlyph, selectedStyle);
            }

            StyledGlyph[] fitted = TextLayout.truncate(item.label.glyphs.TakeWhile(g => !g.isNewline).ToArray(), Math.Max(0, canvas.size.width - 2));
            canvas.drawGlyphs(2, row, fitted, style);
        }

        drawScrollHints(canvas, theme);
    }

    private void drawScrollHints(Canvas canvas, Theme theme)
    {
        if (canvas.size.width < 1 || canvas.size.height < 2)
        {
            return;
        }

        int lastColumn = canvas.size.width - 1;
        if (scrollTop > 0)
        {
            canvas.putGlyph(lastColumn, 0, "▲", 1, theme.text);
        }

        if (scrollTop + visibleRows < items.Count && visibleRows == canvas.size.height)
        {
            canvas.putGlyph(lastColumn, canvas.size.height - 1, "▼", 1, theme.text);
        }
    }

    private void moveSelection(int delta, bool allowWrap = true)
    {
        if (items.Count == 0)
        {
            return;
        }

        int step = Math.Sign(delta);
        int target = selectedIndex;

        // Single steps skip disabled entries; page jumps land on the nearest enabled entry in the same direction.
        int remaining = Math.Abs(delta);
        while (remaining > 0)
        {
            int next = target + step;
            if (next < 0 || next >= items.Count)
            {
                if (!(wrapAround && allowWrap) || Math.Abs(delta) > 1)
                {
                    break;
                }

                next = (next + items.Count) % items.Count;
            }

            target = next;
            if (items[target].enabled)
            {
                remaining--;
            }
            else if (target == selectedIndex)
            {
                break; // full circle, nothing enabled
            }
        }

        if (target != selectedIndex && items[target].enabled)
        {
            selectedIndex = target;
            ensureVisible();
            selectionChanged?.Invoke(selectedIndex);
        }
    }

    private void ensureVisible()
    {
        if (selectedIndex < scrollTop)
        {
            scrollTop = selectedIndex;
        }
        else if (selectedIndex >= scrollTop + visibleRows)
        {
            scrollTop = selectedIndex - visibleRows + 1;
        }

        scrollTop = Math.Max(0, Math.Min(scrollTop, Math.Max(0, items.Count - visibleRows)));
    }
}
