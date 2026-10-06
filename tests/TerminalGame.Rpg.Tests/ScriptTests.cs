using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.Script;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Tests;

public class ConditionTests
{
    [Theory]
    [InlineData("1", true)]
    [InlineData("0", false)]
    [InlineData("gold >= 100", true)]
    [InlineData("gold > 100", false)]
    [InlineData("flag:missing == 0", true)]
    [InlineData("flag:door", true)]
    [InlineData("!flag:door", false)]
    [InlineData("flag:door == 2 && item:potion == 2", true)]
    [InlineData("flag:door == 3 || item:potion < 1", false)]
    [InlineData("!(flag:door == 3 || party:slowpoke)", true)]
    [InlineData("party:hero && level:hero >= 1 && level:slowpoke == 0", true)]
    [InlineData("  flag:door!=2  ", false)]
    [InlineData("gold == -5 || 3 > 2", true)]
    public void evaluatesAgainstSession(string source, bool expected)
    {
        GameSession session = TestContent.session();
        session.flags["door"] = 2;

        Assert.Equal(expected, Condition.parse(source).evaluate(session));
    }

    [Theory]
    [InlineData("")]
    [InlineData("flag:")]
    [InlineData("flag:a ==")]
    [InlineData("(flag:a")]
    [InlineData("flag:a flag:b")]
    [InlineData("money > 3")]
    [InlineData("!= 3")]
    public void rejectsMalformedExpressions(string source)
    {
        Assert.Throws<FormatException>(() => Condition.parse(source));
    }

    [Fact]
    public void blankConditionIsTrue()
    {
        Assert.True(Condition.check(null, TestContent.session()));
        Assert.True(Condition.check("  ", TestContent.session()));
    }

    [Fact]
    public void listsReferences()
    {
        Condition c = Condition.parse("flag:a && (item:potion > 1 || !party:rin) && gold > 2");

        Assert.Equal(new[] { ("flag", "a"), ("item", "potion"), ("party", "rin") }, c.references);
    }
}

public class ScriptTests
{
    [Fact]
    public void runsSayAndBranches()
    {
        GameSession session = TestContent.session();
        ScriptRunner runner = ScriptRunner.forScript(session, "elder");

        Assert.Equal(new SayRequest("Hello.", "Elder"), runner.start());
        ItemGainedRequest gained = Assert.IsType<ItemGainedRequest>(runner.next());
        Assert.Equal(("potion", 2), (gained.item.id, gained.count));
        Assert.Null(runner.next());
        Assert.True(runner.isFinished);
        Assert.Equal(1, session.flags["metElder"]);
        Assert.Equal(4, session.inventory.count("potion"));

        ScriptRunner again = ScriptRunner.forScript(session, "elder");
        again.start();
        Assert.Equal(new SayRequest("Again?", null), again.next());
    }

    [Fact]
    public void choicesHideOptionsWhoseConditionFails()
    {
        GameSession session = TestContent.session();
        ScriptRunner runner = ScriptRunner.forScript(session, "quiz");
        runner.start();

        ChoiceRequest choice = Assert.IsType<ChoiceRequest>(runner.next());
        Assert.Equal(new[] { "Gold", "Friend" }, choice.labels);

        MemberJoinedRequest joined = Assert.IsType<MemberJoinedRequest>(runner.choose(1));
        Assert.Equal("slowpoke", joined.member.def.id);
        Assert.Equal(2, joined.member.level);
        Assert.Null(runner.next());
        Assert.Equal(1, session.flags["quizzes"]);
    }

    [Fact]
    public void newcomersJoinAtLeastAtThePartysAverageLevel()
    {
        GameSession session = TestContent.session();
        session.party[0].gainExp(100_000);
        int lead = session.party[0].level;
        ScriptRunner runner = ScriptRunner.forScript(session, "quiz");
        runner.start();
        runner.next();

        MemberJoinedRequest joined = Assert.IsType<MemberJoinedRequest>(runner.choose(1));

        Assert.Equal(lead, joined.member.level);
    }

    [Fact]
    public void choiceMustBeAnsweredWithChoose()
    {
        GameSession session = TestContent.session();
        ScriptRunner runner = ScriptRunner.forScript(session, "quiz");
        runner.start();
        runner.next();

        Assert.Throws<InvalidOperationException>(() => runner.next());
        Assert.IsType<GoldChangedRequest>(runner.choose(0));
        Assert.Equal(150, session.gold);
    }

    [Fact]
    public void battleVictoryContinuesAndTravelMovesTheParty()
    {
        GameSession session = TestContent.session();
        ScriptRunner runner = ScriptRunner.forScript(session, "ambush");

        BattleRequest battle = Assert.IsType<BattleRequest>(runner.start());
        Assert.Equal(new[] { "blob" }, battle.enemyIds);
        Assert.False(battle.canEscape);
        Assert.Throws<InvalidOperationException>(() => runner.next());

        TravelRequest travel = Assert.IsType<TravelRequest>(runner.battleFinished(BattleOutcome.Victory));
        Assert.Equal("cave", travel.location.id);
        Assert.Equal("cave", session.locationId);
        Assert.Equal(1, session.flags["ambushWon"]);
        Assert.IsType<SayRequest>(runner.next());
    }

    [Fact]
    public void losingAScriptedBattleEndsTheScript()
    {
        GameSession session = TestContent.session();
        ScriptRunner runner = ScriptRunner.forScript(session, "ambush");
        runner.start();

        Assert.Null(runner.battleFinished(BattleOutcome.Defeat));
        Assert.True(runner.isFinished);
        Assert.Equal(0, session.flags["ambushWon"]);
    }

    [Fact]
    public void takeClampsRunCallsAndEndStops()
    {
        GameSession session = TestContent.session();
        session.gold = 20;
        ScriptRunner runner = ScriptRunner.forScript(session, "toll");

        Assert.Equal(new GoldChangedRequest(-20), runner.start());
        ItemLostRequest lost = Assert.IsType<ItemLostRequest>(runner.next());
        Assert.Equal(2, lost.count);
        Assert.Equal(new InnRequest(10), runner.next());
        Assert.Null(runner.next());
        Assert.Equal(0, session.gold);
    }

    [Fact]
    public void runawayRecursionIsCaught()
    {
        ScriptRunner runner = ScriptRunner.forScript(TestContent.session(), "loop");

        Assert.Throws<InvalidOperationException>(() => runner.start());
    }

    [Fact]
    public void validationCatchesBadScripts()
    {
        Dictionary<string, string> files = TestContent.files();
        files[ContentDb.scriptsFile] = """
            {
              "a": [ { "say": "x", "setFlag": "y" } ],
              "b": [ { "giveItem": "nothing" }, { "run": "nowhere" }, { "battle": [] }, { "join": "ghost" } ],
              "c": [ { "if": "flag:x &&", "then": [] }, { "if": "item:fake", "then": [ { "travel": "moon" } ] } ],
              "d": [ { "choice": [ { "label": "x", "then": [ { "shop": "closed" } ] } ] } ],
              "e": [ { } ]
            }
            """;

        ContentException ex = Assert.Throws<ContentException>(() => ContentDb.parse(files));

        string all = string.Join("\n", ex.errors);
        Assert.Contains("script 'a': each command needs exactly one operation, found [say, setFlag]", all);
        Assert.Contains("unknown item 'nothing'", all);
        Assert.Contains("unknown script 'nowhere'", all);
        Assert.Contains("battle without enemies", all);
        Assert.Contains("unknown character 'ghost'", all);
        Assert.Contains("script 'c': expected", all);
        Assert.Contains("unknown item 'fake'", all);
        Assert.Contains("unknown location 'moon'", all);
        Assert.Contains("unknown shop 'closed'", all);
        Assert.Contains("found []", all);
        Assert.Contains("unknown script 'elder'", all); // the town still points at the removed script
    }
}
