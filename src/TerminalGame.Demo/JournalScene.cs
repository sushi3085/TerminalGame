using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;
using TerminalGame.Tui;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>The adventure journal: the current goal and the story so far (newest at the bottom; ↑ ↓ scroll).</summary>
public sealed class JournalScene : Scene
{
    private readonly TextBlock events;

    public JournalScene(GameSession session)
    {
        string goal = session.currentObjective() ?? "[dim]（目前沒有特別的目標。）[/]";
        IReadOnlyList<string> chronicle = session.chronicle();
        events = new TextBlock(chronicle.Count > 0
            ? string.Join("\n", chronicle.Select(e => "・" + e))
            : "[dim]（還沒有任何紀錄。）[/]")
        {
            layoutHeight = Length.fill(),
        };

        StackPanel body = new(Orientation.Vertical);
        body.add(new Label("[gold b]目前目標[/]"));
        body.add(new TextBlock(goal) { layoutHeight = Length.cells(2) });
        body.add(new Spacer { layoutHeight = Length.cells(1) });
        body.add(new Label("[gold b]大事紀[/]"));
        body.add(events);
        body.add(new Label("[dim]↑ ↓ 捲動   Esc 返回[/]", HorizontalAlignment.Right));

        Border panel = new(body, "冒險日誌") { layoutHeight = Length.cells(18), padding = new Thickness(2, 0, 2, 0) };
        root = Ui.centeredPanel(panel, 60);
        if (session.locationId is not null)
        {
            root.theme = Palettes.forName(session.content.location(session.locationId).palette);
        }
    }

    public override bool isTransparent => true;

    public override void onEnter()
    {
        // Start at the newest entries; rendering clamps the offset to the last page.
        events.scrollOffset = int.MaxValue / 2;
    }

    public override void onKey(KeyEvent keyEvent)
    {
        switch (keyEvent.action)
        {
            case GameAction.Up:
                events.scrollOffset = Math.Max(0, events.scrollOffset - 1);
                break;
            case GameAction.Down:
                events.scrollOffset++;
                break;
            case GameAction.Cancel or GameAction.Menu or GameAction.Confirm:
                application!.popScene();
                break;
        }
    }
}
