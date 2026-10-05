using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Tui.Backends;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo.Tests;

/// <summary>Drives the real scenes with key presses on an in-memory terminal.</summary>
public class GameFlowTests
{
    private sealed class EmptyScene : Scene
    {
        public EmptyScene() => root = new Spacer();
    }

    private readonly HeadlessBackend backend = new(80, 24);
    private readonly Application app;
    private readonly ContentDb content = ContentDb.loadDirectory(Path.Combine(AppContext.BaseDirectory, "content"));

    public GameFlowTests()
    {
        app = new Application(backend);
    }

    /// <summary>Lets typewriter text finish.</summary>
    private void pump()
    {
        for (int i = 0; i < 40; i++)
        {
            app.step(0.25);
        }
    }

    /// <summary>Presses Enter until <paramref name="done"/> holds; returns every screen seen on the way.</summary>
    private List<string> pressEnterUntil(Func<bool> done, int maxPresses = 100)
    {
        List<string> screens = new();
        app.step(0);
        pump();
        for (int i = 0; !done(); i++)
        {
            Assert.True(i < maxPresses, "gave up; last screen:\n" + backend.screenText);
            backend.queueKeys(Key.Enter);
            app.step(0);
            pump();
            screens.Add(backend.screenText);
        }

        return screens;
    }

    [Fact]
    public void fightingInTheForestWinsAndReturnsToTown()
    {
        app.pushScene(new TitleScene(content, seed: 1));

        // Title → town intro → choices → forest; Enter on the battle menu always attacks.
        List<string> screens = pressEnterUntil(() => backend.screenText.Contains("歡迎回來"));

        Assert.Contains(screens, s => s.Contains("野生的史萊姆出現了"));
        Assert.Contains(screens, s => s.Contains("戰鬥勝利"));
        Assert.Contains(screens, s => s.Contains("獲得 30 點經驗值"));
        Assert.IsType<TownScene>(app.currentScene);
    }

    [Fact]
    public void losingRaisesFinishedWithDefeat()
    {
        GameSession session = GameSession.newGame(content, seed: 7);
        session.party[0].hp = 1;
        BattleOutcome? outcome = null;
        BattleScene battle = new(session, ["slime"]);
        battle.finished += o => outcome = o;
        app.pushScene(new EmptyScene());
        app.pushScene(battle);

        List<string> screens = pressEnterUntil(() => outcome is not null);

        Assert.Equal(BattleOutcome.Defeat, outcome);
        Assert.Contains(screens, s => s.Contains("全員倒下了"));
        app.step(0);
        Assert.IsType<EmptyScene>(app.currentScene);
    }

    [Fact]
    public void statusPanelShowsDerivedStatsAndEquipment()
    {
        GameSession session = GameSession.newGame(content, seed: 1);
        app.pushScene(new EmptyScene());
        app.pushScene(new StatusScene(session));
        app.step(0);

        string screen = backend.screenText;
        Assert.Contains("Lv.3", screen);
        Assert.Contains("木劍", screen);
        Assert.Contains($"攻擊 {session.party[0].stats.attack,3}", screen);
        Assert.Contains("傷藥 x3", screen);
    }
}
