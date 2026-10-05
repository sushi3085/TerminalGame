using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Tui;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>A transparent overlay showing one member at a time (← → to page); the scene below stays visible.</summary>
public sealed class StatusScene : Scene
{
    private readonly GameSession session;
    private readonly int memberIndex;

    public StatusScene(GameSession session, int memberIndex = 0)
    {
        this.session = session;
        this.memberIndex = memberIndex;
        PartyMember member = session.party[memberIndex];
        StatBlock s = member.stats;

        string expLine = member.expToNext > 0
            ? $"EXP {member.exp}/{member.expToNext}  [dim](距下一級 {member.expToNext - member.exp})[/]"
            : "EXP [gold]MAX[/]";

        StackPanel body = new(Orientation.Vertical);
        body.add(new Label($"[gold b]{member.name}[/]  Lv.{member.level}"));
        body.add(Ui.hpBar(member.hp, s.maxHp));
        body.add(Ui.mpBar(member.mp, s.maxMp));
        body.add(new Label(expLine));
        body.add(new Label($"攻擊 {s.attack,3}   防禦 {s.defense,3}"));
        body.add(new Label($"魔力 {s.magic,3}   速度 {s.speed,3}"));
        foreach ((EquipSlot slot, string label) in EquipScene.slotLabels)
        {
            body.add(new Label($"[dim]{label}[/] {member.equippedIn(slot)?.name ?? "[dim]—[/]"}"));
        }

        string skills = string.Join("、", member.skills.Select(k => k.name));
        body.add(new Label($"[dim]技能[/] {(skills.Length > 0 ? skills : "[dim]—[/]")}"));
        body.add(new Spacer { layoutHeight = Length.cells(1) });

        string items = string.Join("  ", session.inventory.entries.Select(e => $"{session.content.item(e.itemId).name} x{e.count}"));
        body.add(new Label($"[gold]{session.gold}[/] G   [green]{(items.Length > 0 ? items : "[dim]沒有道具[/]")}[/]"));
        string pager = session.party.Count > 1 ? $"← → {memberIndex + 1}/{session.party.Count}   " : "";
        body.add(new Label($"[dim]{pager}Esc 返回[/]", HorizontalAlignment.Right));

        Border panel = new(body, "角色狀態") { layoutHeight = Length.cells(17), padding = new Thickness(2, 0, 2, 0) };
        root = Ui.centeredPanel(panel, 50);
        if (session.locationId is not null)
        {
            root.theme = Palettes.forName(session.content.location(session.locationId).palette);
        }
    }

    public override bool isTransparent => true;

    public override void onKey(KeyEvent keyEvent)
    {
        int count = session.party.Count;
        switch (keyEvent.action)
        {
            case GameAction.Left when count > 1:
                application!.replaceScene(new StatusScene(session, (memberIndex + count - 1) % count));
                break;
            case GameAction.Right when count > 1:
                application!.replaceScene(new StatusScene(session, (memberIndex + 1) % count));
                break;
            case GameAction.Cancel or GameAction.Menu or GameAction.Confirm:
                application!.popScene();
                break;
        }
    }
}
