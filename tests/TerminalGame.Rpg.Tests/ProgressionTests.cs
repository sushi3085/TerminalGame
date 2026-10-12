using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;

namespace TerminalGame.Rpg.Tests;

public class ProgressionTests
{
    [Theory]
    [InlineData(5, 5, 1.0)]
    [InlineData(1, 9, 1.0)] // fighting above your level never pays less (or more)
    [InlineData(8, 5, 1.0)] // within the grace window
    [InlineData(9, 5, 0.8)]
    [InlineData(10, 5, 0.6)]
    [InlineData(12, 5, 0.2)]
    [InlineData(30, 1, 0.2)] // floor
    public void expTapersOffBeyondTheGraceWindow(int memberLevel, int enemyLevel, double expected)
    {
        Assert.Equal(expected, Progression.expRate(memberLevel, enemyLevel), 6);
    }

    [Fact]
    public void encountersTaperOffMoreGentlyAndNeverStop()
    {
        Assert.Equal(1.0, Progression.encounterScale(13, 10), 6);
        Assert.Equal(0.75, Progression.encounterScale(14, 10), 6);
        Assert.Equal(0.5, Progression.encounterScale(15, 10), 6);
        Assert.Equal(0.25, Progression.encounterScale(30, 10), 6);
    }

    [Fact]
    public void overLevelledMembersEarnLessButGoldIsUnchanged()
    {
        GameSession session = TestContent.session();
        session.addMember("slowpoke", 6); // 5 levels above the blob (level 1) → 60%
        int goldBefore = session.gold;
        BattleEngine engine = new(session, [session.content.enemy("blob")]);
        engine.start();
        while (engine.currentActor!.side != Side.Party)
        {
            engine.execute(engine.decideEnemyAction());
        }

        engine.enemies[0].hp = 1;
        IReadOnlyList<BattleEvent> events = engine.execute(new AttackAction(engine.currentActor, engine.enemies[0]));

        VictoryEvent victory = Assert.Single(events.OfType<VictoryEvent>());
        Assert.Equal(20, victory.exp);
        Assert.Equal(new[] { ("hero", 20), ("slowpoke", 12) }, victory.shares.Select(s => (s.member.def.id, s.exp)));
        Assert.Equal(12, session.party[1].exp);
        Assert.Equal(goldBefore + 7, session.gold);
    }

    [Fact]
    public void anOutgrownAreaHasFewerRandomBattlesButSearchingStillFindsOne()
    {
        GameSession session = TestContent.session();
        LocationDef woods = session.content.location("woods");
        Assert.Equal(1, session.areaLevel(woods));
        Assert.Equal(0.5, session.encounterRate(woods), 6);

        session.party[0].gainExp(100_000);
        Assert.Equal(0.5 * Progression.minEncounterRate, session.encounterRate(woods), 6);
        Assert.NotNull(session.rollEncounter(woods, forced: true));
    }
}
