using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Tests;

public class BattleTests
{
    private static (GameSession session, BattleEngine engine) setUp(params string[] enemyIds) => setUp(1, enemyIds);

    private static (GameSession session, BattleEngine engine) setUp(int seed, params string[] enemyIds)
    {
        GameSession session = TestContent.session(seed);
        BattleEngine engine = new(session, enemyIds.Select(session.content.enemy));
        engine.start();
        return (session, engine);
    }

    /// <summary>Party members always attack the first living enemy; enemies use their AI.</summary>
    private static List<BattleEvent> autoPlay(BattleEngine engine, int maxTurns = 500)
    {
        List<BattleEvent> log = new();
        for (int turn = 0; engine.outcome == BattleOutcome.Ongoing; turn++)
        {
            Assert.True(turn < maxTurns, "battle did not finish");
            Combatant actor = engine.currentActor!;
            BattleAction action = actor.side == Side.Enemies
                ? engine.decideEnemyAction()
                : new AttackAction(actor, engine.opponentsOf(actor)[0]);
            log.AddRange(engine.execute(action));
        }

        return log;
    }

    [Fact]
    public void fasterCombatantActsFirst()
    {
        GameSession session = TestContent.session();
        BattleEngine engine = new(session, [session.content.enemy("blob")]);

        IReadOnlyList<BattleEvent> events = engine.start();

        Assert.Equal(new RoundStartedEvent(1), events[0]);
        Assert.Equal(Side.Party, engine.currentActor!.side);

        (_, BattleEngine vsWisp) = setUp("wisp");
        Assert.Equal("Wisp", vsWisp.currentActor!.name);
    }

    [Fact]
    public void attackDamageFollowsFormula()
    {
        (_, BattleEngine engine) = setUp("blob");
        Combatant hero = engine.currentActor!;
        Combatant blob = engine.enemies[0];

        IReadOnlyList<BattleEvent> events = engine.execute(new AttackAction(hero, blob));

        Assert.Equal(new AttackEvent(hero), events[0]);
        DamageEvent hit = Assert.IsType<DamageEvent>(events[1]);
        double baseDamage = 12 - 2 / 2.0; // attack 10+2 (stick) − blob defense / 2
        Assert.InRange(hit.amount, (int)Math.Floor(baseDamage * 0.85), (int)Math.Ceiling(baseDamage * 1.5 * 1.15));
        Assert.Equal(30 - hit.amount, blob.hp);
        Assert.Equal(blob.hp, hit.remainingHp);
        Assert.Same(blob, engine.currentActor);
    }

    [Fact]
    public void damageIsAtLeastOne()
    {
        Random random = new(3);
        EffectDef punch = new() { kind = EffectKind.Physical, power = 100 };
        StatBlock weak = new() { attack = 1 };
        StatBlock wall = new() { defense = 500 };

        for (int i = 0; i < 100; i++)
        {
            Assert.Equal(1, DamageFormula.rollDamage(weak, wall, punch, false, random).amount);
        }
    }

    [Fact]
    public void guardingHalvesDamage()
    {
        Random random = new(5);
        EffectDef hit = new() { kind = EffectKind.Magical, power = 100 };
        StatBlock user = new() { magic = 100 };

        int normal = DamageFormula.rollDamage(user, default, hit, false, new Random(5)).amount;
        int guarded = DamageFormula.rollDamage(user, default, hit, true, random).amount;

        Assert.Equal(Math.Round(normal * DamageFormula.guardMultiplier), guarded, 1.0);
    }

    [Fact]
    public void enemyAiTargetsTheParty()
    {
        (_, BattleEngine engine) = setUp("blob");
        engine.execute(new GuardAction(engine.currentActor!));

        BattleAction action = engine.decideEnemyAction();

        AttackAction attack = Assert.IsType<AttackAction>(action);
        Assert.Equal(Side.Party, attack.target.side);
    }

    [Fact]
    public void skillsCostMpAndMustBeKnownAndAffordable()
    {
        (GameSession session, BattleEngine engine) = setUp("titan");
        Combatant hero = engine.currentActor!;
        Combatant titan = engine.enemies[0];

        Assert.Throws<InvalidOperationException>(() => engine.execute(new SkillAction(hero, session.content.skill("quake"), null)));

        IReadOnlyList<BattleEvent> events = engine.execute(new SkillAction(hero, session.content.skill("bolt"), titan));

        Assert.Equal(new SkillUsedEvent(hero, session.content.skill("bolt"), 5), events[0]);
        Assert.Equal(5, session.party[0].mp);
        Assert.IsType<DamageEvent>(events[1]);

        hero.mp = 0;
        engine.execute(engine.decideEnemyAction()); // titan's turn — hero has 50 HP, titan's attack kills it
        Assert.Equal(BattleOutcome.Defeat, engine.outcome);
    }

    [Fact]
    public void cannotActOutOfTurn()
    {
        (_, BattleEngine engine) = setUp("blob");
        Combatant blob = engine.enemies[0];

        Assert.Throws<InvalidOperationException>(() => engine.execute(new GuardAction(blob)));
        Assert.Throws<InvalidOperationException>(() => engine.decideEnemyAction());
    }

    [Fact]
    public void itemsAreConsumedAndHealUpToMax()
    {
        (GameSession session, BattleEngine engine) = setUp("blob");
        Combatant hero = engine.currentActor!;
        hero.hp = 40;

        IReadOnlyList<BattleEvent> events = engine.execute(new ItemAction(hero, session.content.item("potion"), hero));

        Assert.Equal(new ItemUsedEvent(hero, session.content.item("potion"), 1), events[0]);
        Assert.Equal(new HealEvent(hero, 10, 50), events[1]);
        Assert.Equal(1, session.inventory.count("potion"));
    }

    [Fact]
    public void cannotUseMissingItem()
    {
        (GameSession session, BattleEngine engine) = setUp("blob");
        Combatant hero = engine.currentActor!;

        Assert.Throws<InvalidOperationException>(() => engine.execute(new ItemAction(hero, session.content.item("ether"), hero)));
        Assert.Throws<InvalidOperationException>(() => engine.execute(new ItemAction(hero, session.content.item("blade"), hero)));
    }

    [Fact]
    public void victoryGrantsRewardsAndEndsBattle()
    {
        (GameSession session, BattleEngine engine) = setUp("blob");
        Combatant hero = engine.currentActor!;
        Combatant blob = engine.enemies[0];
        blob.hp = 1;

        IReadOnlyList<BattleEvent> events = engine.execute(new AttackAction(hero, blob));

        Assert.Contains(new DefeatedEvent(blob), events);
        VictoryEvent victory = Assert.Single(events.OfType<VictoryEvent>());
        Assert.Equal(20, victory.exp);
        Assert.Equal(7, victory.gold);
        Assert.Equal("potion", Assert.Single(victory.drops).id);
        Assert.Equal(2, Assert.Single(events.OfType<LevelUpEvent>()).levelUp.newLevel); // 20 ≥ expToNext(1) = 16
        Assert.Equal(BattleOutcome.Victory, engine.outcome);
        Assert.Null(engine.currentActor);
        Assert.Equal(107, session.gold);
        Assert.Equal(3, session.inventory.count("potion"));
        Assert.Throws<InvalidOperationException>(() => engine.execute(new GuardAction(hero)));
    }

    [Fact]
    public void wipingThePartyIsADefeat()
    {
        (GameSession session, BattleEngine engine) = setUp("titan");
        engine.execute(new GuardAction(engine.currentActor!));

        IReadOnlyList<BattleEvent> events = engine.execute(engine.decideEnemyAction());

        Assert.Equal(BattleOutcome.Defeat, engine.outcome);
        Assert.IsType<PartyDefeatedEvent>(events[^1]);
        Assert.True(session.isPartyDefeated);
        Assert.Equal(0, session.gold - 100); // no rewards
    }

    [Fact]
    public void duplicateEnemiesGetLetterSuffixes()
    {
        (_, BattleEngine engine) = setUp("blob", "wisp", "blob");

        Assert.Equal(new[] { "Blob A", "Wisp", "Blob B" }, engine.enemies.Select(e => e.name));
    }

    [Fact]
    public void areaSkillHitsEveryLivingEnemy()
    {
        GameSession session = TestContent.session();
        session.party[0].gainExp(10_000); // learn everything, plenty of MP
        BattleEngine engine = new(session, new[] { "blob", "blob", "blob" }.Select(session.content.enemy));
        engine.start();
        engine.enemies[1].hp = 0;

        IReadOnlyList<BattleEvent> events = engine.execute(new SkillAction(engine.party[0], session.content.skill("quake"), null));

        Assert.Equal(new[] { engine.enemies[0], engine.enemies[2] }, events.OfType<DamageEvent>().Select(d => d.target));
    }

    [Fact]
    public void deadSingleTargetIsReplaced()
    {
        (_, BattleEngine engine) = setUp("blob", "blob");
        Combatant dead = engine.enemies[0];
        dead.hp = 0;

        IReadOnlyList<BattleEvent> events = engine.execute(new AttackAction(engine.currentActor!, dead));

        Assert.Same(engine.enemies[1], Assert.Single(events.OfType<DamageEvent>()).target);
    }

    [Fact]
    public void deadCombatantsSkipTheirTurn()
    {
        (_, BattleEngine engine) = setUp("blob", "blob");
        engine.enemies[0].hp = 0;

        engine.execute(new GuardAction(engine.currentActor!));

        Assert.Same(engine.enemies[1], engine.currentActor);
    }

    [Fact]
    public void escapeEventuallySucceedsUnlessForbidden()
    {
        for (int seed = 0; ; seed++)
        {
            Assert.True(seed < 50);
            (_, BattleEngine engine) = setUp(seed, "blob");
            EscapeEvent escape = Assert.IsType<EscapeEvent>(engine.execute(new EscapeAction(engine.currentActor!))[0]);
            if (escape.succeeded)
            {
                Assert.Equal(BattleOutcome.Escaped, engine.outcome);
                break;
            }

            Assert.Equal(BattleOutcome.Ongoing, engine.outcome);
        }

        GameSession session = TestContent.session();
        BattleEngine boss = new(session, [session.content.enemy("blob")], canEscape: false);
        boss.start();
        Assert.Throws<InvalidOperationException>(() => boss.execute(new EscapeAction(boss.currentActor!)));
    }

    [Fact]
    public void sameSeedGivesTheSameBattle()
    {
        string run(int seed) => string.Join("\n", autoPlay(setUp(seed, "blob", "wisp").engine));

        Assert.Equal(run(42), run(42));
        Assert.NotEqual(run(42), run(43));
    }

    [Fact]
    public void manySimulatedBattlesAllTerminateConsistently()
    {
        for (int seed = 0; seed < 200; seed++)
        {
            (GameSession session, BattleEngine engine) = setUp(seed, "blob", "wisp");
            List<BattleEvent> log = autoPlay(engine);

            Assert.NotEqual(BattleOutcome.Ongoing, engine.outcome);
            Assert.Equal(engine.outcome == BattleOutcome.Victory, log.OfType<VictoryEvent>().Any());
            Assert.Equal(engine.outcome == BattleOutcome.Defeat, session.isPartyDefeated);
            Assert.All(engine.party.Concat(engine.enemies), c => Assert.InRange(c.hp, 0, c.stats.maxHp));
        }
    }
}
