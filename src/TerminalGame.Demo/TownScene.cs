using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.State;
using TerminalGame.Tui;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

public sealed class TownScene : Scene
{
    private static readonly (string speaker, string text)[] intro =
    {
        ("[cyan]長老[/]", "歡迎來到[gold]晨曦鎮[/]，勇者。森林裡的史萊姆最近愈來愈躁動了，也許是[red]魔王[/]的力量正在甦醒……"),
        ("[cyan]長老[/]", "你的劍已經磨亮了吧？去森林討伐牠們，順便把這個帶上。（獲得 [green]傷藥[/] x3）\n記得，受傷了就回來休息。"),
    };

    private enum ReturnAction
    {
        None,
        ReopenChoices,
        WelcomeBack,
        Rescued,
    }

    private readonly GameSession state;
    private readonly DialogueBox dialogue = new();
    private int introIndex;
    private ReturnAction returnAction;

    public TownScene(GameSession state)
    {
        this.state = state;

        ArtBlock town = new(
            """
            [dim]      *      .          *        .     [/]
            [orange]        /\      [/][dim]              [/][cyan]  ~ ~ ~ ~[/]
            [orange]       /  \     [/][gold]   ___[/]       [cyan] ~ ~ ~ ~ ~[/]
            [orange]      /____\    [/][gold]  |[/][red]田[/][gold]|[/][gold]_[/]
            [orange]      | [] |    [/][gold]  |___|[/]
            [green] ,,,,,,|____|,,,,,,,,,,,,,,,,,,,,,,,,,[/]
            """);

        dialogue.layoutHeight = Length.cells(6);
        dialogue.completed += showNext;

        StackPanel layout = new(Orientation.Vertical);
        layout.add(new Border(town, "[gold]晨曦鎮[/]") { layoutHeight = Length.fill() });
        layout.add(dialogue);

        root = layout;
        setFocus(dialogue);
    }

    public override void onEnter() => showNext();

    public override void onResume()
    {
        setFocus(dialogue);
        ReturnAction action = returnAction;
        returnAction = ReturnAction.None;
        if (action == ReturnAction.WelcomeBack)
        {
            state.restoreParty();
            dialogue.show("歡迎回來。傷口還好嗎？\n要休息的話，隨時告訴我。", "[cyan]長老[/]");
        }
        else if (action == ReturnAction.Rescued)
        {
            state.restoreParty();
            dialogue.show("你倒在森林裡，是路過的獵人把你揹回來的。\n別逞強，先好好休息吧。", "[cyan]長老[/]");
        }
        else if (action == ReturnAction.ReopenChoices)
        {
            openChoices();
        }
    }

    public override void onKey(KeyEvent keyEvent)
    {
        if (keyEvent.action == GameAction.Menu)
        {
            application!.pushScene(new StatusScene(state));
        }
    }

    private void showNext()
    {
        if (introIndex < intro.Length)
        {
            (string speaker, string text) = intro[introIndex++];
            dialogue.show(text, speaker);
            return;
        }

        openChoices();
    }

    private void openChoices()
    {
        MenuList menu = new(new[]
        {
            MenuItem.of("前往森林 [dim](戰鬥)[/]"),
            MenuItem.of("查看狀態"),
            MenuItem.of("回到標題"),
        });
        Border frame = new(menu, "接下來？") { layoutWidth = Length.cells(28) };
        menu.confirmed += index =>
        {
            closeModal(frame);
            switch (index)
            {
                case 0:
                    BattleScene battle = new(state, ["slime"]);
                    battle.finished += outcome =>
                        returnAction = outcome == BattleOutcome.Defeat ? ReturnAction.Rescued : ReturnAction.WelcomeBack;
                    application!.pushScene(battle);
                    break;
                case 1:
                    returnAction = ReturnAction.ReopenChoices;
                    application!.pushScene(new StatusScene(state));
                    break;
                default:
                    application!.replaceScene(new TitleScene(state.content));
                    break;
            }
        };
        showModal(frame, HorizontalAlignment.Center, VerticalAlignment.Center);
    }
}
