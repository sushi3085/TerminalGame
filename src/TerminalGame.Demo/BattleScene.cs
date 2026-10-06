using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Tui;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>
/// Battle UI. All rules live in <see cref="BattleEngine"/>; this scene only collects the player's commands and plays
/// back the events the engine returns, one message page at a time. HP/MP bars show the values as of the page being
/// displayed, not the engine's live values, so the bars move in step with the narration.
/// </summary>
public sealed class BattleScene : Scene
{
    /// <summary>Mutable because a boss's next phase takes over its column.</summary>
    private sealed class EnemyView(EnemyCombatant combatant, Label name, ArtBlock art, ProgressBar hp)
    {
        public EnemyCombatant combatant { get; set; } = combatant;
        public Label name { get; } = name;
        public ArtBlock art { get; } = art;
        public ProgressBar hp { get; } = hp;
    }

    /// <summary>Solo: name, HP bar, MP bar. Party of 2+: one compact text line per member (bars are null).</summary>
    private sealed record MemberView(Combatant combatant, Label name, ProgressBar? hp, ProgressBar? mp);

    private readonly GameSession session;
    private readonly BattleEngine engine;
    private readonly List<EnemyView> enemyViews = new();
    private readonly List<MemberView> memberViews = new();
    private readonly Dictionary<Combatant, (int hp, int mp)> shown = new();
    private readonly Dictionary<Combatant, List<StatusKind>> shownStatuses = new();
    private readonly MenuList commands = new();
    private readonly DialogueBox messages = new();
    private Action? afterMessage;

    public BattleScene(Game game, IEnumerable<string> enemyIds, bool canEscape = true, string? palette = null)
        : this(game.session, enemyIds, canEscape, palette)
    {
    }

    public BattleScene(GameSession session, IEnumerable<string> enemyIds, bool canEscape = true, string? palette = null)
    {
        this.session = session;
        engine = new BattleEngine(session, enemyIds.Select(session.content.enemy), canEscape);
        foreach (Combatant c in engine.party.Concat(engine.enemies))
        {
            shown[c] = (c.hp, c.mp);
            shownStatuses[c] = new List<StatusKind>();
        }

        StackPanel enemyRow = new(Orientation.Horizontal);
        foreach (Combatant enemy in engine.enemies)
        {
            EnemyCombatant e = (EnemyCombatant)enemy;
            Label name = new($"[red b]{e.name}[/]  Lv.{e.def.level}", HorizontalAlignment.Center);
            ArtBlock art = new(string.Join("\n", e.def.art)) { layoutHeight = Length.cells(Math.Max(1, e.def.art.Count)) };
            ProgressBar hp = Ui.hpBar(e.hp, e.stats.maxHp);
            hp.layoutWidth = Length.cells(Math.Min(24, 76 / engine.enemies.Count - 2));

            StackPanel column = new(Orientation.Vertical) { layoutWidth = Length.fill() };
            column.add(name);
            column.add(new Spacer());
            column.add(art);
            column.add(new Spacer());
            column.add(Ui.centered(hp));
            enemyRow.add(column);
            enemyViews.Add(new EnemyView(e, name, art, hp));
        }

        StackPanel partyPanel = new(Orientation.Vertical);
        bool solo = engine.party.Count == 1;
        foreach (Combatant member in engine.party)
        {
            Label name = new();
            partyPanel.add(name);
            if (solo)
            {
                ProgressBar hp = Ui.hpBar(member.hp, member.stats.maxHp);
                ProgressBar mp = Ui.mpBar(member.mp, member.stats.maxMp);
                partyPanel.add(hp);
                partyPanel.add(mp);
                memberViews.Add(new MemberView(member, name, hp, mp));
            }
            else
            {
                memberViews.Add(new MemberView(member, name, null, null));
            }
        }

        commands.confirmed += onCommand;
        messages.completed += () =>
        {
            Action? next = afterMessage;
            afterMessage = null;
            next?.Invoke();
        };

        StackPanel bottom = new(Orientation.Horizontal) { layoutHeight = Length.cells(8) };
        bottom.add(new Border(commands, "指令") { layoutWidth = Length.cells(16), padding = new Thickness(1, 0, 0, 0) });
        bottom.add(messages);
        bottom.add(new Border(partyPanel, "隊伍") { layoutWidth = Length.cells(solo ? 24 : 32) });

        StackPanel layout = new(Orientation.Vertical);
        layout.add(new Border(enemyRow, canEscape ? "[red]遭遇戰[/]" : "[red b]強敵[/]") { layoutHeight = Length.fill() });
        layout.add(bottom);
        layout.theme = Palettes.forName(palette);
        root = layout;
    }

    public BattleOutcome outcome => engine.outcome;

    /// <summary>Raised once the last message of the battle is dismissed, just before the scene pops itself.</summary>
    public event Action<BattleOutcome>? finished;

    public override void onUpdate(double deltaSeconds) => session.playSeconds += deltaSeconds;

    public override void onEnter()
    {
        syncBars();
        engine.start();
        say(BattleNarrator.encounter(engine.enemies), continueBattle);
    }

    private void syncBars()
    {
        foreach (EnemyView view in enemyViews)
        {
            EnemyCombatant enemy = view.combatant;
            int hp = shown[enemy].hp;
            view.hp.value = hp;
            if (hp <= 0)
            {
                view.art.setArt("");
                view.name.setText($"[dim]{enemy.name}[/]");
            }
            else
            {
                view.name.setText($"[red b]{enemy.name}[/]  Lv.{enemy.def.level}{StatusText.tags(shownStatuses[enemy])}");
            }
        }

        foreach (MemberView view in memberViews)
        {
            Combatant c = view.combatant;
            (int hp, int mp) = shown[c];
            string marker = ReferenceEquals(c, engine.currentActor) && c.side == Side.Party ? "[gold]▶[/]" : " ";
            string statuses = StatusText.tags(shownStatuses[c]);
            if (view.hp is not null && view.mp is not null)
            {
                view.hp.value = hp;
                view.mp.value = mp;
                view.name.setText(hp > 0 ? $"[gold]{c.name}[/]{statuses}" : $"[dim]{c.name}[/]");
                continue;
            }

            int maxHp = c.stats.maxHp;
            string hpColor = hp <= 0 ? "dim" : hp * 4 <= maxHp ? "red" : hp * 2 <= maxHp ? "gold" : "white";
            string name = hp > 0 ? $"[gold]{c.name}[/]" : $"[dim]{c.name}[/]";
            view.name.setText($"{marker}{name} [{hpColor}]{hp,3}/{maxHp,-3}[/] [dim]MP[/]{mp,3}{statuses}");
        }
    }

    /// <summary>Shows a message in the log window; <paramref name="then"/> runs once the player dismisses it.</summary>
    private void say(string markup, Action then)
    {
        afterMessage = then;
        setFocus(messages);
        messages.show(markup);
    }

    /// <summary>Narrates <paramref name="events"/> page by page, applying each page's HP/MP changes as it appears.</summary>
    private void play(IReadOnlyList<BattleEvent> events, Action then)
    {
        IReadOnlyList<BattlePage> pages = BattleNarrator.narrate(events);
        int index = 0;

        void next()
        {
            while (index < pages.Count)
            {
                BattlePage page = pages[index++];
                apply(page.events);
                if (page.markup.Length > 0)
                {
                    say(page.markup, next);
                    return;
                }
            }

            then();
        }

        next();
    }

    private void apply(IEnumerable<BattleEvent> events)
    {
        foreach (BattleEvent e in events)
        {
            switch (e)
            {
                case DamageEvent d:
                    shown[d.target] = (d.remainingHp, shown[d.target].mp);
                    break;
                case PoisonDamageEvent p:
                    shown[p.target] = (p.remainingHp, shown[p.target].mp);
                    break;
                case StatusAppliedEvent s when !shownStatuses[s.target].Contains(s.kind):
                    shownStatuses[s.target].Add(s.kind);
                    break;
                case StatusRemovedEvent s:
                    shownStatuses[s.target].Remove(s.kind);
                    break;
                case DefeatedEvent d:
                    shownStatuses[d.target].Clear();
                    break;
                case HealEvent h:
                    shown[h.target] = (h.remainingHp, shown[h.target].mp);
                    break;
                case ReviveEvent r:
                    shown[r.target] = (r.remainingHp, shown[r.target].mp);
                    break;
                case PhaseChangedEvent p:
                    EnemyView column = enemyViews.First(v => v.combatant == p.previous);
                    column.combatant = p.next;
                    column.art.setArt(string.Join("\n", p.next.def.art));
                    column.art.layoutHeight = Length.cells(Math.Max(1, p.next.def.art.Count));
                    column.hp.maximum = p.next.stats.maxHp;
                    shown[p.next] = (p.next.hp, p.next.mp);
                    shownStatuses[p.next] = new List<StatusKind>();
                    break;
                case MpRestoreEvent m:
                    shown[m.target] = (shown[m.target].hp, m.remainingMp);
                    break;
                case SkillUsedEvent s:
                    shown[s.actor] = (shown[s.actor].hp, s.remainingMp);
                    break;
                case LevelUpEvent:
                    // Level-ups raise max HP/MP; show the live values and maxima from here on.
                    foreach (MemberView view in memberViews)
                    {
                        view.hp?.maximum = view.combatant.stats.maxHp;
                        view.mp?.maximum = view.combatant.stats.maxMp;
                        shown[view.combatant] = (view.combatant.hp, view.combatant.mp);
                    }

                    break;
            }
        }

        syncBars();
    }

    /// <summary>Runs enemy turns until it is a party member's turn or the battle is over.</summary>
    private void continueBattle()
    {
        if (engine.outcome != BattleOutcome.Ongoing)
        {
            finished?.Invoke(engine.outcome);
            application!.popScene();
            return;
        }

        Combatant actor = engine.currentActor!;
        if (actor.side == Side.Enemies)
        {
            play(engine.execute(engine.decideEnemyAction()), continueBattle);
            return;
        }

        syncBars(); // move the ▶ marker
        beginPlayerTurn();
    }

    private void beginPlayerTurn()
    {
        Combatant actor = engine.currentActor!;
        int keep = commands.selectedIndex;
        commands.setItems(new[]
        {
            MenuItem.of("攻擊"),
            MenuItem.of("技能", engine.skillsOf(actor).Any(s => usable(actor, s))),
            MenuItem.of("道具", usableItems().Any()),
            MenuItem.of("防禦"),
            MenuItem.of("逃跑", engine.canEscape),
        });
        commands.select(keep);
        setFocus(commands);
        if (engine.party.Count > 1)
        {
            messages.show($"[gold]{actor.name}[/]要怎麼做？");
        }
    }

    /// <summary>Affordable and with someone to aim at (a revive needs a fallen ally).</summary>
    private bool usable(Combatant actor, SkillDef skill) =>
        engine.canAfford(actor, skill) && engine.candidatesFor(actor, skill.target, skill.effect).Count > 0;

    private IEnumerable<(ItemDef item, int count)> usableItems() =>
        session.inventory.entries
            .Select(e => (item: session.content.item(e.itemId), e.count))
            .Where(e => e.item.usableInBattle);

    private void act(BattleAction action) => play(engine.execute(action), continueBattle);

    private void onCommand(int index)
    {
        Combatant actor = engine.currentActor!;
        switch (index)
        {
            case 0:
                chooseTarget(actor, TargetKind.SingleEnemy, null, target => act(new AttackAction(actor, target!)));
                break;
            case 1:
                chooseSkill(actor);
                break;
            case 2:
                chooseItem(actor);
                break;
            case 3:
                act(new GuardAction(actor));
                break;
            default:
                act(new EscapeAction(actor));
                break;
        }
    }

    private void chooseSkill(Combatant actor)
    {
        IReadOnlyList<SkillDef> skills = engine.skillsOf(actor);
        pick(
            "技能",
            skills.Select(s => MenuItem.of($"{s.name} [dim]MP{s.mpCost}[/]", usable(actor, s))),
            i => chooseTarget(actor, skills[i].target, skills[i].effect, target => act(new SkillAction(actor, skills[i], target))));
    }

    private void chooseItem(Combatant actor)
    {
        List<(ItemDef item, int count)> items = usableItems().ToList();
        pick(
            "道具",
            items.Select(e => MenuItem.of($"{e.item.name} x{e.count}", engine.candidatesFor(actor, e.item.target, e.item.effect!).Count > 0)),
            i => chooseTarget(actor, items[i].item.target, items[i].item.effect!, target => act(new ItemAction(actor, items[i].item, target))));
    }

    /// <summary>Asks for a target only when there is a real choice; otherwise picks the obvious one.</summary>
    private void chooseTarget(Combatant actor, TargetKind kind, EffectDef? effect, Action<Combatant?> then)
    {
        IReadOnlyList<Combatant> candidates = kind switch
        {
            TargetKind.SingleEnemy => engine.opponentsOf(actor),
            TargetKind.SingleAlly when effect is not null => engine.candidatesFor(actor, kind, effect),
            TargetKind.SingleAlly => engine.alliesOf(actor),
            _ => [],
        };

        if (candidates.Count == 0)
        {
            then(null);
        }
        else if (candidates.Count == 1)
        {
            then(candidates[0]);
        }
        else
        {
            pick(
                "對象",
                candidates.Select(c => MenuItem.of($"{BattleNarrator.nameOf(c)} [dim]HP {c.hp}/{c.stats.maxHp}[/]{StatusText.tags(c.statuses.Select(st => st.kind))}")),
                i => then(candidates[i]));
        }
    }

    /// <summary>Popups sit over the command window, bottom left, leaving the enemies and messages visible.</summary>
    private void pick(string title, IEnumerable<MenuItem> items, Action<int> onPick) =>
        Ui.showPicker(this, title, items, onPick, horizontal: HorizontalAlignment.Left, vertical: VerticalAlignment.Bottom);
}
