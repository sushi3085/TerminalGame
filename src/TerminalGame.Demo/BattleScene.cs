using TerminalGame.Tui;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

public sealed class BattleScene : Scene
{
    private const int enemyMaxHp = 40;
    private const int skillCost = 5;

    private readonly GameState state;
    private readonly ProgressBar enemyBar;
    private readonly ProgressBar heroHp;
    private readonly ProgressBar heroMp;
    private readonly MenuList commands;
    private readonly DialogueBox messages = new();
    private int enemyHp = enemyMaxHp;
    private Action? afterMessage;

    public BattleScene(GameState state)
    {
        this.state = state;

        ArtBlock slime = new(
            """
            [green]      _.---._      [/]
            [green]    .'       '.    [/]
            [green]   /  [/][white]●[/][green]     [/][white]●[/][green]  \   [/]
            [green]  |     [/][red]\_/[/][green]     |  [/]
            [green]   \_____________/  [/]
            """);
        enemyBar = new ProgressBar(enemyHp, enemyMaxHp, "HP") { layoutWidth = Length.cells(24) };
        enemyBar.setGradient(Color.rgb(200, 50, 50), Color.rgb(210, 170, 40), Color.rgb(40, 160, 70));

        StackPanel enemyColumn = new(Orientation.Vertical);
        enemyColumn.add(new Label("[red b]史萊姆[/]  Lv.2", HorizontalAlignment.Center));
        enemyColumn.add(new Spacer());
        slime.layoutHeight = Length.cells(5);
        enemyColumn.add(slime);
        enemyColumn.add(new Spacer());
        enemyColumn.add(centered(enemyBar));

        commands = new MenuList();
        commands.confirmed += onCommand;
        heroHp = new ProgressBar(0, state.hero.maxHp, "HP");
        heroHp.setGradient(Color.rgb(200, 50, 50), Color.rgb(210, 170, 40), Color.rgb(40, 160, 70));
        heroMp = new ProgressBar(0, state.hero.maxMp, "MP") { fillColor = Color.rgb(60, 110, 210) };

        StackPanel party = new(Orientation.Vertical);
        party.add(new Label($"[gold]{state.hero.name}[/]"));
        party.add(heroHp);
        party.add(heroMp);

        messages.completed += () =>
        {
            Action? next = afterMessage;
            afterMessage = null;
            next?.Invoke();
        };

        StackPanel bottom = new(Orientation.Horizontal) { layoutHeight = Length.cells(8) };
        bottom.add(new Border(commands, "指令") { layoutWidth = Length.cells(16), padding = new Thickness(1, 0, 0, 0) });
        bottom.add(messages);
        bottom.add(new Border(party, "隊伍") { layoutWidth = Length.cells(24) });

        StackPanel layout = new(Orientation.Vertical);
        layout.add(new Border(enemyColumn, "[red]遭遇戰[/]") { layoutHeight = Length.fill() });
        layout.add(bottom);
        root = layout;
    }

    public override void onEnter()
    {
        syncBars();
        say("[red]野生的史萊姆[/]出現了！", beginPlayerTurn);
    }

    public override void onUpdate(double deltaSeconds) => syncBars();

    private static Widget centered(Widget child)
    {
        StackPanel row = new(Orientation.Horizontal) { layoutHeight = Length.cells(1) };
        row.add(new Spacer());
        row.add(child);
        row.add(new Spacer());
        return row;
    }

    private void syncBars()
    {
        enemyBar.value = enemyHp;
        heroHp.value = state.hero.hp;
        heroMp.value = state.hero.mp;
    }

    /// <summary>Shows a message in the log window; <paramref name="then"/> runs once the player dismisses it.</summary>
    private void say(string markup, Action then)
    {
        afterMessage = then;
        setFocus(messages);
        messages.show(markup);
    }

    private void beginPlayerTurn()
    {
        int keep = commands.selectedIndex;
        commands.setItems(new[]
        {
            MenuItem.of("攻擊"),
            MenuItem.of($"[magenta]火球術[/] [dim]MP{skillCost}[/]", state.hero.mp >= skillCost),
            MenuItem.of($"傷藥 x{state.potions}", state.potions > 0),
            MenuItem.of("逃跑"),
        });
        commands.select(keep);
        setFocus(commands);
    }

    private void onCommand(int index)
    {
        switch (index)
        {
            case 0:
                hitEnemy(state.random.Next(8, 15), "勇者揮劍斬擊！");
                break;
            case 1:
                state.hero.mp -= skillCost;
                hitEnemy(state.random.Next(18, 25), "勇者詠唱[magenta]火球術[/]！");
                break;
            case 2:
                state.potions--;
                state.hero.hp = Math.Min(state.hero.maxHp, state.hero.hp + 25);
                say("勇者喝下[green]傷藥[/]，恢復了 [gold]25[/] 點 HP。", enemyTurn);
                break;
            default:
                say("勇者逃跑了！", () => application!.popScene());
                break;
        }
    }

    private void hitEnemy(int damage, string prefix)
    {
        enemyHp = Math.Max(0, enemyHp - damage);
        string text = $"{prefix}\n對[red]史萊姆[/]造成 [gold b]{damage}[/] 點傷害！";
        say(text, () =>
        {
            if (enemyHp > 0)
            {
                enemyTurn();
                return;
            }

            state.experience += 30;
            say("史萊姆倒下了！\n獲得 [gold]30[/] 點經驗值。", () => application!.popScene());
        });
    }

    private void enemyTurn()
    {
        int damage = state.random.Next(5, 11);
        state.hero.hp = Math.Max(0, state.hero.hp - damage);
        say($"[red]史萊姆[/]撲了過來！\n勇者受到 [red b]{damage}[/] 點傷害。", () =>
        {
            if (state.hero.hp > 0)
            {
                beginPlayerTurn();
                return;
            }

            say("勇者倒下了……\n在長老的照料下，你在鎮上醒了過來。", () => application!.popScene());
        });
    }
}
