using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.Script;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;
using TerminalGame.Tui;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>
/// One scene for every place in the world. It shows the location's art and description, offers its exits and spots
/// as an action menu, and plays event scripts by answering the <see cref="ScriptRequest"/>s a
/// <see cref="ScriptRunner"/> produces. Moving to another location replaces this scene with a new one.
/// </summary>
public sealed class LocationScene : Scene
{
    private readonly Game game;
    private readonly GameSession session;
    private readonly LocationDef location;
    private readonly bool rollEncounterOnArrival;
    private readonly string? openingMessage;
    private readonly DialogueBox messages = new();
    private readonly MenuList actions = new();
    private readonly Label partyLine = new();
    private readonly List<Action> actionHandlers = new();
    private Action? afterMessage;
    private Action? onResumeAction;
    private ScriptRunner? script;
    private Action? afterScript;
    private bool travelPending;

    /// <param name="rollEncounterOnArrival">True when the party just walked in (a random battle may happen on the way).</param>
    /// <param name="openingMessage">Shown before the description, e.g. after waking up from a defeat.</param>
    public LocationScene(Game game, bool rollEncounterOnArrival = false, string? openingMessage = null)
    {
        this.game = game;
        session = game.session;
        location = session.currentLocation();
        this.rollEncounterOnArrival = rollEncounterOnArrival;
        this.openingMessage = openingMessage;

        ArtBlock art = new(string.Join("\n", location.art)) { layoutHeight = Length.fill() };
        StackPanel scenery = new(Orientation.Vertical);
        scenery.add(art);
        scenery.add(partyLine);

        actions.confirmed += i => actionHandlers[i]();
        messages.completed += () =>
        {
            Action? next = afterMessage;
            afterMessage = null;
            next?.Invoke();
        };

        StackPanel bottom = new(Orientation.Horizontal) { layoutHeight = Length.cells(8) };
        bottom.add(new Border(actions, "行動") { layoutWidth = Length.cells(26), padding = new Thickness(1, 0, 0, 0) });
        bottom.add(messages);

        StackPanel layout = new(Orientation.Vertical);
        layout.add(new Border(scenery, $"[gold]{location.name}[/]") { layoutHeight = Length.fill() });
        layout.add(bottom);
        layout.theme = Palettes.forName(location.palette);
        root = layout;
    }

    public LocationDef place => location;

    public override void onEnter()
    {
        refreshStatus();
        Action arrive = () => runArrival();
        Action describe = location.description.Length > 0 ? () => say(location.description, null, arrive) : arrive;
        if (openingMessage is not null)
        {
            say(openingMessage, null, describe);
        }
        else
        {
            describe();
        }
    }

    public override void onResume()
    {
        refreshStatus();
        Action? next = onResumeAction;
        onResumeAction = null;
        (next ?? showActions)();
    }

    public override void onUpdate(double deltaSeconds) => session.playSeconds += deltaSeconds;

    public override void onKey(KeyEvent keyEvent)
    {
        if (keyEvent.action == GameAction.Menu && ReferenceEquals(focusedWidget, actions) && !hasModal && script is null)
        {
            openPartyMenu();
        }
    }

    private void refreshStatus()
    {
        string members = string.Join("  ", session.party.Select(m =>
            m.isAlive ? $"[gold]{m.name}[/] {m.hp}/{m.stats.maxHp}" : $"[dim]{m.name} 倒下[/]"));
        partyLine.setText($"{members}   [gold]{session.gold}[/] G");
    }

    private void say(string markup, string? speaker, Action then)
    {
        afterMessage = then;
        setFocus(messages);
        messages.show(markup, speaker);
    }

    private void runArrival()
    {
        string? trigger = session.arrivalScript(location);
        if (trigger is not null)
        {
            runScript(trigger, showActions);
            return;
        }

        if (rollEncounterOnArrival && session.rollEncounter(location) is EncounterDef encounter)
        {
            startBattle(encounter.enemies, canEscape: true, _ => afterRandomBattle());
            return;
        }

        showActions();
    }

    // ── Action menu ──────────────────────────────────────────────────────────────────────────

    private void showActions()
    {
        List<(MenuItem item, Action handler)> entries = new();
        foreach (SpotDef spot in session.visibleSpots(location))
        {
            entries.Add((MenuItem.of(spot.label), () => runScript(spot.script, showActions)));
        }

        foreach (ExitDef exit in session.visibleExits(location))
        {
            entries.Add((MenuItem.of($"[cyan]{session.exitLabel(exit)}[/]"), () => travel(exit)));
        }

        if (location.hasEncounters)
        {
            entries.Add((MenuItem.of("四處探索 [dim](戰鬥)[/]"), explore));
        }

        if (session.availableBanters(location).Count > 0)
        {
            string mark = session.hasNewBanter(location) ? " [gold]![/]" : "";
            entries.Add((MenuItem.of("隊伍閒聊" + mark), banter));
        }

        entries.Add((MenuItem.of("隊伍選單"), openPartyMenu));

        int keep = actions.selectedIndex;
        actionHandlers.Clear();
        actionHandlers.AddRange(entries.Select(e => e.handler));
        actions.setItems(entries.Select(e => e.item));
        actions.select(Math.Min(keep, entries.Count - 1));
        setFocus(actions);
    }

    private void travel(ExitDef exit)
    {
        session.travel(exit);
        application!.replaceScene(new LocationScene(game, rollEncounterOnArrival: true));
    }

    private void explore()
    {
        say("你在附近四處探索……", null, () =>
        {
            EncounterDef encounter = session.rollEncounter(location, forced: true)!;
            startBattle(encounter.enemies, canEscape: true, _ => afterRandomBattle());
        });
    }

    /// <summary>Clears the stale "searching…" line so the window does not suggest the search is still going on.</summary>
    private void afterRandomBattle()
    {
        messages.show("");
        showActions();
    }

    private void banter()
    {
        BanterDef? chosen = session.takeBanter(location);
        if (chosen is null)
        {
            showActions();
            return;
        }

        runScript(chosen.script, showActions);
    }

    private void openPartyMenu()
    {
        onResumeAction = showActions;
        application!.pushScene(new PartyMenuScene(game));
    }

    // ── Battles ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Pushes a battle. <paramref name="after"/> runs on victory or escape; a defeat sends the party to the respawn point.</summary>
    private void startBattle(IReadOnlyList<string> enemyIds, bool canEscape, Action<BattleOutcome> after)
    {
        BattleScene battle = new(game, enemyIds, canEscape, location.palette);
        battle.finished += outcome => onResumeAction = outcome == BattleOutcome.Defeat ? onDefeat : () => after(outcome);
        application!.pushScene(battle);
    }

    private void onDefeat()
    {
        int lost = session.applyDefeat();
        string where = session.currentLocation().name;
        string message = $"……醒來時，你已經回到了{where}。" + (lost > 0 ? $"\n[dim](失去了 {lost} G)[/]" : "");
        application!.replaceScene(new LocationScene(game, openingMessage: message));
    }

    // ── Scripts ──────────────────────────────────────────────────────────────────────────────

    private void runScript(string scriptId, Action then)
    {
        script = ScriptRunner.forScript(session, scriptId);
        afterScript = then;
        handle(script.start());
    }

    private void continueScript() => handle(script!.next());

    private void handle(ScriptRequest? request)
    {
        refreshStatus();
        switch (request)
        {
            case null:
                script = null;
                if (travelPending)
                {
                    travelPending = false;
                    application!.replaceScene(new LocationScene(game));
                    return;
                }

                afterScript?.Invoke();
                break;
            case SayRequest s:
                say(s.text, s.speaker, continueScript);
                break;
            case ChoiceRequest c:
                // Cancelling picks the last option, which by convention is the "no / leave" answer.
                Ui.showPicker(this, "選擇", c.labels.Select(l => MenuItem.of(l)), i => handle(script!.choose(i)),
                    () => handle(script!.choose(c.labels.Count - 1)), width: 28, HorizontalAlignment.Right, VerticalAlignment.Bottom);
                break;
            case ItemGainedRequest g:
                say($"獲得了[green]{g.item.name}[/]{(g.count > 1 ? $" x{g.count}" : "")}！", null, continueScript);
                break;
            case ItemLostRequest l:
                say($"交出了[green]{l.item.name}[/]{(l.count > 1 ? $" x{l.count}" : "")}。", null, continueScript);
                break;
            case GoldChangedRequest { amount: >= 0 } g:
                say($"獲得了 [gold]{g.amount}[/] G！", null, continueScript);
                break;
            case GoldChangedRequest g:
                say($"付出了 [gold]{-g.amount}[/] G。", null, continueScript);
                break;
            case MemberJoinedRequest j:
                say($"[gold b]{j.member.name}[/]加入了隊伍！", null, continueScript);
                break;
            case BattleRequest b:
                startBattle(b.enemyIds, b.canEscape, outcome => handle(script!.battleFinished(outcome)));
                break;
            case ShopRequest s:
                onResumeAction = continueScript;
                application!.pushScene(new ShopScene(game, s.shop));
                break;
            case InnRequest i:
                stayAtInn(i.price, continueScript);
                break;
            case TravelRequest:
                travelPending = true;
                continueScript();
                break;
            default:
                throw new InvalidOperationException($"unhandled script request {request.GetType().Name}");
        }
    }

    private void stayAtInn(int price, Action then)
    {
        const string keeper = "[cyan]旅館老闆[/]";
        say($"歡迎光臨！住一晚 [gold]{price}[/] G，要休息嗎？", keeper, () => Ui.confirm(this, $"住宿 {price} G？", yes =>
        {
            if (!yes)
            {
                say("隨時歡迎再來。", keeper, then);
                return;
            }

            if (session.gold < price)
            {
                say("……金幣好像不太夠呢。", keeper, then);
                return;
            }

            session.gold -= price;
            session.restoreParty();
            session.respawnLocationId = location.id;
            refreshStatus();
            say("（好好地睡了一晚，HP 與 MP 全部恢復了。）", null, () => Ui.confirm(this, "要記錄冒險嗎？", save =>
            {
                if (!save)
                {
                    then();
                    return;
                }

                try
                {
                    game.save();
                    say("冒險已經記錄下來了。", null, then);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    say($"[red]無法存檔：{ex.Message}[/]", null, then);
                }
            }));
        }));
    }
}
