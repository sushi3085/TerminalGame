using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Tests;

public class AutoPolicyTests
{
    [Fact]
    public void healsTheMostHurtAllyFirst()
    {
        GameSession session = TestContent.session();
        session.party[0].gainExp(100); // learns cure
        BattleEngine engine = new(session, [session.content.enemy("blob")]);
        engine.start();
        session.party[0].hp = 5;

        SkillAction action = Assert.IsType<SkillAction>(new AutoPolicy().decide(engine, session));

        Assert.Equal("cure", action.skill.id);
        Assert.Same(engine.party[0], action.target);
    }

    [Fact]
    public void fallsBackToPotionsWithoutHealingMagic()
    {
        GameSession session = TestContent.session();
        BattleEngine engine = new(session, [session.content.enemy("blob")]);
        engine.start();
        session.party[0].hp = 5;

        ItemAction action = Assert.IsType<ItemAction>(new AutoPolicy().decide(engine, session));

        Assert.Equal("potion", action.item.id);
        Assert.IsType<AttackAction>(new AutoPolicy { useItems = false, spendMp = false }.decide(engine, session));
    }

    [Fact]
    public void usesAreaSkillsOnGroupsAndFinishesOffWeakEnemies()
    {
        GameSession session = TestContent.session();
        session.party[0].gainExp(10_000); // knows quake
        BattleEngine engine = new(session, new[] { "blob", "blob", "blob" }.Select(session.content.enemy));
        engine.start();

        SkillAction area = Assert.IsType<SkillAction>(new AutoPolicy().decide(engine, session));
        Assert.Equal("quake", area.skill.id);

        engine.enemies[0].hp = 0;
        engine.enemies[1].hp = 0;
        engine.enemies[2].hp = 3;
        AttackAction finish = Assert.IsType<AttackAction>(new AutoPolicy { spendMp = false }.decide(engine, session));
        Assert.Same(engine.enemies[2], finish.target);
    }
}

/// <summary>
/// Guards chapter 1's balance against accidental content changes. Thresholds are deliberately loose; the full report
/// is <c>dotnet run --project tools/TerminalGame.Sim</c>.
/// </summary>
public class BalanceTests
{
    private const int runs = 200;

    private static readonly ContentDb content = ContentDb.loadDirectory(Path.Combine(AppContext.BaseDirectory, "content"));

    private static GameSession party(int seed, params (string id, int level, string[] gear)[] members)
    {
        GameSession session = new(content, seed);
        foreach ((string id, int level, string[] gear) in members)
        {
            PartyMember member = session.addMember(id, level);
            foreach (string itemId in gear)
            {
                member.equip(content.item(itemId));
            }

            member.restore();
        }

        session.inventory.add("potion", 5);
        return session;
    }

    private static double winRate(Func<int, GameSession> build, string[] enemies, AutoPolicy policy)
    {
        int wins = 0;
        for (int seed = 0; seed < runs; seed++)
        {
            GameSession session = build(seed);
            BattleEngine engine = new(session, enemies.Select(content.enemy));
            engine.start();
            while (engine.outcome == BattleOutcome.Ongoing && engine.round < 60)
            {
                engine.execute(engine.currentActor!.side == Side.Party ? policy.decide(engine, session) : engine.decideEnemyAction());
            }

            wins += engine.outcome == BattleOutcome.Victory ? 1 : 0;
        }

        return wins / (double)runs;
    }

    [Theory]
    [InlineData("woodsEdge")]
    [InlineData("woodsPath")]
    public void soloRandomBattlesAreWinnable(string locationId)
    {
        foreach (EncounterDef encounter in content.location(locationId).encounters)
        {
            double rate = winRate(seed => party(seed, ("hero", 3, ["woodenSword", "clothTunic"])), [.. encounter.enemies], new AutoPolicy { spendMp = false });
            Assert.True(rate >= 0.95, $"{string.Join("+", encounter.enemies)} at {locationId}: {rate:P0}");
        }
    }

    [Fact]
    public void rinsRescueIsSafeAtTheExpectedLevel()
    {
        double rate = winRate(
            seed => party(seed, ("hero", 4, ["woodenSword", "clothTunic"]), ("rin", 4, ["huntersBow", "clothTunic"])),
            ["wolf", "wolf"],
            new AutoPolicy());

        Assert.True(rate >= 0.95, $"rescue: {rate:P0}");
    }

    [Fact]
    public void forestLordIsBeatableButNotFreeWhenUnderLevelled()
    {
        (string, int, string[])[] at(int level) =>
            [("hero", level, ["bronzeSword", "leatherArmor"]), ("rin", level, ["longBow", "clothTunic", "leatherCap"])];
        string[] fight = ["venomShroom", "forestLord", "venomShroom"];

        double expected = winRate(seed => party(seed, at(7)), fight, new AutoPolicy());
        double low = winRate(seed => party(seed, at(5)), fight, new AutoPolicy());

        Assert.True(expected >= 0.8, $"boss at Lv7: {expected:P0}");
        Assert.True(low <= 0.6, $"boss at Lv5 should be a real risk: {low:P0}");
    }
}
