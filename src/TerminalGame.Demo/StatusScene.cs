using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Tui;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>A transparent overlay: the scene below stays visible around the panel.</summary>
public sealed class StatusScene : Scene
{
    private static readonly (EquipSlot slot, string label)[] slotLabels =
    {
        (EquipSlot.Weapon, "武器"),
        (EquipSlot.Body, "身體"),
        (EquipSlot.Head, "頭部"),
        (EquipSlot.Accessory, "飾品"),
    };

    public StatusScene(GameSession session)
    {
        StackPanel body = new(Orientation.Vertical);
        foreach (PartyMember member in session.party)
        {
            addMember(body, member);
        }

        string items = string.Join("  ", session.inventory.entries.Select(e => $"{session.content.item(e.itemId).name} x{e.count}"));
        body.add(new Label($"[gold]{session.gold}[/] G   [green]{(items.Length > 0 ? items : "[dim]沒有道具[/]")}[/]"));
        body.add(new Label("[dim]Esc 返回[/]", HorizontalAlignment.Right));

        int height = session.party.Count * 9 + 4;
        Border panel = new(body, "角色狀態") { layoutHeight = Length.cells(height), padding = new Thickness(2, 0, 2, 0) };

        StackPanel middle = new(Orientation.Vertical) { layoutWidth = Length.cells(46) };
        middle.add(new Spacer());
        middle.add(panel);
        middle.add(new Spacer());

        StackPanel row = new(Orientation.Horizontal);
        row.add(new Spacer());
        row.add(middle);
        row.add(new Spacer());

        root = row;
    }

    public override bool isTransparent => true;

    public override void onKey(KeyEvent keyEvent)
    {
        if (keyEvent.action is GameAction.Cancel or GameAction.Menu or GameAction.Confirm)
        {
            application!.popScene();
        }
    }

    /// <summary>Nine rows per member: name, HP, MP, EXP, two stat rows, two equipment rows, a gap.</summary>
    private static void addMember(StackPanel body, PartyMember member)
    {
        StatBlock s = member.stats;
        ProgressBar hp = new(member.hp, s.maxHp, "HP");
        hp.setGradient(Color.rgb(200, 50, 50), Color.rgb(210, 170, 40), Color.rgb(40, 160, 70));
        ProgressBar mp = new(member.mp, s.maxMp, "MP") { fillColor = Color.rgb(60, 110, 210) };

        string expLine = member.expToNext > 0
            ? $"EXP {member.exp}/{member.expToNext}  [dim](距下一級 {member.expToNext - member.exp})[/]"
            : "EXP [gold]MAX[/]";

        body.add(new Label($"[gold b]{member.name}[/]  Lv.{member.level}"));
        body.add(hp);
        body.add(mp);
        body.add(new Label(expLine));
        body.add(new Label($"攻擊 {s.attack,3}   防禦 {s.defense,3}"));
        body.add(new Label($"魔力 {s.magic,3}   速度 {s.speed,3}"));
        for (int i = 0; i < slotLabels.Length; i += 2)
        {
            body.add(new Label($"{equipCell(member, slotLabels[i])}   {equipCell(member, slotLabels[i + 1])}"));
        }

        body.add(new Spacer { layoutHeight = Length.cells(1) });
    }

    private static string equipCell(PartyMember member, (EquipSlot slot, string label) entry)
    {
        ItemDef? item = member.equippedIn(entry.slot);
        string name = item is null ? "[dim]—[/]" : item.name;
        return $"[dim]{entry.label}[/] {name}";
    }
}
