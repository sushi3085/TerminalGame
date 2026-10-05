using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;
using TerminalGame.Tui;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>The field menu (Tab / M): use items and healing skills, change equipment, view status.</summary>
public sealed class PartyMenuScene : Scene
{
    private readonly Game game;
    private readonly GameSession session;
    private readonly MenuList commands;
    private readonly StackPanel memberLines = new(Orientation.Vertical);
    private readonly Label footer = new();
    private readonly Label message = new();

    public PartyMenuScene(Game game)
    {
        this.game = game;
        session = game.session;

        commands = new MenuList(new[]
        {
            MenuItem.of("道具"),
            MenuItem.of("技能"),
            MenuItem.of("裝備"),
            MenuItem.of("狀態"),
            MenuItem.of("關閉"),
        });
        commands.confirmed += onCommand;
        commands.cancelled += () => application!.popScene();

        StackPanel info = new(Orientation.Vertical);
        info.add(memberLines);
        info.add(new Spacer());
        info.add(message);
        info.add(footer);

        StackPanel body = new(Orientation.Horizontal);
        body.add(new Border(commands) { layoutWidth = Length.cells(12), padding = new Thickness(1, 0, 0, 0) });
        body.add(new Border(info, "隊伍") { padding = new Thickness(1, 0, 1, 0) });
        body.layoutHeight = Length.cells(Math.Max(9, session.party.Count + 6));

        root = Ui.centeredPanel(body, 64);
        root.theme = Palettes.forName(session.currentLocation().palette);
        setFocus(commands);
    }

    public override bool isTransparent => true;

    public override void onEnter() => refresh();

    public override void onResume() => refresh();

    private void refresh()
    {
        memberLines.clearChildren();
        foreach (PartyMember member in session.party)
        {
            memberLines.add(new Label(Ui.memberLine(member)));
        }

        TimeSpan time = TimeSpan.FromSeconds(session.playSeconds);
        footer.setText($"[gold]{session.gold}[/] G    [dim]遊玩時間 {(int)time.TotalHours:00}:{time.Minutes:00}[/]");
        setFocus(commands);
    }

    private void onCommand(int index)
    {
        message.setText("");
        switch (index)
        {
            case 0:
                useItem();
                break;
            case 1:
                castSkill();
                break;
            case 2:
                application!.pushScene(new EquipScene(game));
                break;
            case 3:
                application!.pushScene(new StatusScene(session));
                break;
            default:
                application!.popScene();
                break;
        }
    }

    private void useItem()
    {
        List<(ItemDef item, int count)> items = session.inventory.entries
            .Select(e => (item: session.content.item(e.itemId), e.count))
            .Where(e => FieldRules.usableInField(e.item))
            .ToList();
        if (items.Count == 0)
        {
            message.setText("[dim]沒有可以使用的道具。[/]");
            return;
        }

        Ui.showPicker(this, "道具", items.Select(e => MenuItem.of($"{e.item.name} x{e.count}")), i =>
        {
            ItemDef item = items[i].item;
            chooseMember(item.effect!, target => report(FieldRules.useItem(session, item, target), item.effect!));
        });
    }

    private void castSkill()
    {
        List<PartyMember> casters = session.party.Where(m => m.isAlive && m.skills.Any(FieldRules.usableInField)).ToList();
        if (casters.Count == 0)
        {
            message.setText("[dim]沒有人會在戰鬥外使用的技能。[/]");
            return;
        }

        void pickSkill(PartyMember caster)
        {
            List<SkillDef> skills = caster.skills.Where(FieldRules.usableInField).ToList();
            Ui.showPicker(this, $"{caster.name}的技能",
                skills.Select(s => MenuItem.of($"{s.name} [dim]MP{s.mpCost}[/]", caster.mp >= s.mpCost)),
                i => chooseMember(skills[i].effect, target => report(FieldRules.castSkill(session, caster, skills[i], target), skills[i].effect)));
        }

        if (casters.Count == 1)
        {
            pickSkill(casters[0]);
        }
        else
        {
            Ui.showPicker(this, "誰來施展？", casters.Select(c => MenuItem.of(Ui.memberLine(c))), i => pickSkill(casters[i]), width: 44);
        }
    }

    /// <summary>Picks who receives an effect; members it would not help are greyed out.</summary>
    private void chooseMember(EffectDef effect, Action<PartyMember> then)
    {
        IReadOnlyList<PartyMember> party = session.party;
        Ui.showPicker(this, "對誰使用？",
            party.Select(m => MenuItem.of(Ui.memberLine(m), FieldRules.wouldHelp(effect, m))),
            i => then(party[i]), width: 44);
    }

    private void report(IReadOnlyList<(PartyMember member, int amount)> results, EffectDef effect)
    {
        string what = effect.kind == EffectKind.RestoreMp ? "MP" : "HP";
        message.setText(string.Join("  ", results.Select(r => $"{r.member.name}恢復了 [gold]{r.amount}[/] 點 {what}。")));
        refresh();
    }
}
