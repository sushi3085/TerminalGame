using TerminalGame.Rpg.State;
using TerminalGame.Tui;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>Small widget helpers shared by the game scenes.</summary>
public static class Ui
{
    public static ProgressBar hpBar(int value, int maximum)
    {
        ProgressBar bar = new(value, maximum, "HP");
        bar.setGradient(Color.rgb(200, 50, 50), Color.rgb(210, 170, 40), Color.rgb(40, 160, 70));
        return bar;
    }

    public static ProgressBar mpBar(int value, int maximum) => new(value, maximum, "MP") { fillColor = Color.rgb(60, 110, 210) };

    /// <summary>Centers <paramref name="child"/> horizontally in a row as tall as the child.</summary>
    public static Widget centered(Widget child)
    {
        StackPanel row = new(Orientation.Horizontal) { layoutHeight = child.layoutHeight };
        row.add(new Spacer());
        row.add(child);
        row.add(new Spacer());
        return row;
    }

    /// <summary>Centers <paramref name="child"/> both ways (for panels shown over a transparent scene).</summary>
    public static Widget centeredPanel(Widget child, int width)
    {
        StackPanel column = new(Orientation.Vertical) { layoutWidth = Length.cells(width) };
        column.add(new Spacer());
        column.add(child);
        column.add(new Spacer());

        StackPanel row = new(Orientation.Horizontal);
        row.add(new Spacer());
        row.add(column);
        row.add(new Spacer());
        return row;
    }

    /// <summary>
    /// A popup list. Confirming closes it and calls <paramref name="onPick"/> with the index; cancelling closes it
    /// and calls <paramref name="onCancel"/>. Returns the list so callers can listen to selection changes.
    /// </summary>
    public static MenuList showPicker(
        Scene scene,
        string title,
        IEnumerable<MenuItem> items,
        Action<int> onPick,
        Action? onCancel = null,
        int width = 30,
        HorizontalAlignment horizontal = HorizontalAlignment.Center,
        VerticalAlignment vertical = VerticalAlignment.Center)
    {
        MenuList list = new(items);
        Border frame = new(list, title)
        {
            layoutWidth = Length.cells(width),
            layoutHeight = Length.cells(Math.Min(12, Math.Max(1, list.entries.Count) + 2)),
        };
        list.confirmed += i =>
        {
            scene.closeModal(frame);
            onPick(i);
        };
        list.cancelled += () =>
        {
            scene.closeModal(frame);
            onCancel?.Invoke();
        };
        scene.showModal(frame, horizontal, vertical);
        return list;
    }

    /// <summary>A yes/no popup; cancel counts as no.</summary>
    public static void confirm(Scene scene, string title, Action<bool> then) =>
        showPicker(scene, title, [MenuItem.of("是"), MenuItem.of("否")], i => then(i == 0), () => then(false), width: 24);

    /// <summary>"艾倫 HP 52/60 MP 15/20" with the HP colored by how hurt the member is.</summary>
    public static string memberLine(PartyMember m)
    {
        int maxHp = m.stats.maxHp;
        string color = !m.isAlive ? "dim" : m.hp * 4 <= maxHp ? "red" : m.hp * 2 <= maxHp ? "gold" : "white";
        return $"[gold]{m.name}[/] Lv{m.level}  [{color}]HP {m.hp}/{maxHp}[/]  MP {m.mp}/{m.stats.maxMp}";
    }
}
