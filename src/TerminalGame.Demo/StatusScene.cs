using TerminalGame.Tui;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>A transparent overlay: the scene below stays visible around the panel.</summary>
public sealed class StatusScene : Scene
{
    public StatusScene(GameState state)
    {
        PartyMember hero = state.hero;

        ProgressBar hp = new(hero.hp, hero.maxHp, "HP");
        hp.setGradient(Color.rgb(200, 50, 50), Color.rgb(210, 170, 40), Color.rgb(40, 160, 70));
        ProgressBar mp = new(hero.mp, hero.maxMp, "MP") { fillColor = Color.rgb(60, 110, 210) };

        StackPanel body = new(Orientation.Vertical);
        body.add(new Label($"[gold b]{hero.name}[/]  Lv.{hero.level}"));
        body.add(new Spacer());
        body.add(hp);
        body.add(mp);
        body.add(new Spacer());
        body.add(new Label($"[green]傷藥[/] x{state.potions}      EXP {state.experience}"));
        body.add(new Label("[dim]Esc 返回[/]", HorizontalAlignment.Right));

        Border panel = new(body, "角色狀態") { layoutHeight = Length.cells(10), padding = new Thickness(2, 0, 2, 0) };

        StackPanel middle = new(Orientation.Vertical) { layoutWidth = Length.cells(38) };
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
}
