using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Tests;

public class StatusTests
{
    private const string skills = """
        [
          { "id": "flame", "name": "Flame", "target": "SingleEnemy", "effect": { "kind": "Magical", "power": 100, "element": "Fire" } },
          { "id": "venom", "name": "Venom", "target": "SingleEnemy", "effect": { "kind": "Status", "status": { "kind": "Poison", "turns": 3 } } },
          { "id": "lull", "name": "Lull", "target": "SingleEnemy", "effect": { "kind": "Status", "status": { "kind": "Sleep", "turns": 2 } } },
          { "id": "numb", "name": "Numb", "target": "SingleEnemy", "effect": { "kind": "Status", "status": { "kind": "Paralysis", "turns": 3 } } },
          { "id": "rally", "name": "Rally", "target": "Self", "effect": { "kind": "Status", "status": { "kind": "AttackUp", "turns": 2 } } },
          { "id": "sap", "name": "Sap", "target": "Self", "effect": { "kind": "Status", "status": { "kind": "AttackDown", "turns": 2 } } }
        ]
        """;

    private const string items = """
        [ { "id": "antidote", "name": "Antidote", "kind": "Consumable", "effect": { "kind": "Status", "cures": [ "Poison" ] } } ]
        """;

    private const string characters = """
        [ { "id": "tester", "name": "Tester",
            "baseStats": { "maxHp": 100, "maxMp": 50, "attack": 10, "magic": 20, "speed": 10 }, "growth": {},
            "skills": [ { "skillId": "flame" }, { "skillId": "venom" }, { "skillId": "lull" }, { "skillId": "numb" },
                        { "skillId": "rally" }, { "skillId": "sap" } ] } ]
        """;

    private const string enemies = """
        [
          { "id": "dummy", "name": "Dummy", "stats": { "maxHp": 500, "speed": 1 }, "weakTo": [ "Fire" ] },
          { "id": "stone", "name": "Stone", "stats": { "maxHp": 500, "speed": 1 }, "resists": [ "Fire" ], "immuneTo": [ "Sleep" ] },
          { "id": "plain", "name": "Plain", "stats": { "maxHp": 500, "speed": 1 } },
          { "id": "frail", "name": "Frail", "stats": { "maxHp": 5, "speed": 1 }, "exp": 3 },
          { "id": "viper", "name": "Viper", "stats": { "maxHp": 500, "speed": 50 }, "actions": [ { "skillId": "venom" } ] },
          { "id": "brute", "name": "Brute", "stats": { "maxHp": 500, "attack": 1, "speed": 50 }, "actions": [ { "skillId": "rally" } ] }
        ]
        """;

    private static Dictionary<string, string> files() => new()
    {
        [ContentDb.skillsFile] = skills,
        [ContentDb.itemsFile] = items,
        [ContentDb.charactersFile] = characters,
        [ContentDb.enemiesFile] = enemies,
    };

    private static (GameSession session, BattleEngine engine) battle(string enemyId, int seed = 7)
    {
        ContentDb content = ContentDb.parse(files());
        GameSession session = new(content, seed);
        session.addMember("tester");
        BattleEngine engine = new(session, [content.enemy(enemyId)]);
        engine.start();
        return (session, engine);
    }

    /// <summary>The hero's action, then any enemy turns, until it is the hero's turn again (or the battle is over).</summary>
    private static IReadOnlyList<BattleEvent> heroRound(BattleEngine engine, BattleAction action)
    {
        List<BattleEvent> events = engine.execute(action).ToList();
        while (engine.outcome == BattleOutcome.Ongoing && engine.currentActor!.side == Side.Enemies)
        {
            events.AddRange(engine.execute(engine.decideEnemyAction()));
        }

        return events;
    }

    private static IReadOnlyList<BattleEvent> cast(BattleEngine engine, string skillId)
    {
        Combatant actor = engine.currentActor!;
        SkillDef skill = engine.skillsOf(actor).Single(s => s.id == skillId);
        return heroRound(engine, new SkillAction(actor, skill, engine.opponentsOf(actor)[0]));
    }

    private static IReadOnlyList<BattleEvent> guard(BattleEngine engine) => heroRound(engine, new GuardAction(engine.currentActor!));

    [Fact]
    public void elementsScaleDamageAndAreReported()
    {
        (_, BattleEngine weak) = battle("dummy");
        (_, BattleEngine resisted) = battle("stone");
        (_, BattleEngine plain) = battle("plain");

        DamageEvent onWeak = cast(weak, "flame").OfType<DamageEvent>().First();
        DamageEvent onResisted = cast(resisted, "flame").OfType<DamageEvent>().First();
        DamageEvent onPlain = cast(plain, "flame").OfType<DamageEvent>().First();

        Assert.Equal(Effectiveness.Weak, onWeak.effectiveness);
        Assert.Equal(Effectiveness.Resisted, onResisted.effectiveness);
        Assert.Equal(Effectiveness.Normal, onPlain.effectiveness);
        // Same seed → same roll, so only the element differs.
        Assert.InRange(onWeak.amount, onPlain.amount * 1.5 - 1, onPlain.amount * 1.5 + 1);
        Assert.InRange(onResisted.amount, onPlain.amount * 0.5 - 1, onPlain.amount * 0.5 + 1);
    }

    [Fact]
    public void poisonHurtsAtTheEndOfEachOfItsHoldersTurnsUntilItWearsOff()
    {
        (_, BattleEngine engine) = battle("plain");
        Combatant enemy = engine.enemies[0];

        List<BattleEvent> log = cast(engine, "venom").ToList(); // the hero is faster: the enemy acts right after
        Assert.Contains(new StatusAppliedEvent(enemy, StatusKind.Poison, 3), log);
        for (int i = 0; i < 2; i++)
        {
            log.AddRange(guard(engine));
        }

        List<PoisonDamageEvent> ticks = log.OfType<PoisonDamageEvent>().ToList();
        Assert.Equal(3, ticks.Count);
        Assert.All(ticks, t => Assert.Equal(50, t.amount));
        Assert.Contains(new StatusRemovedEvent(enemy, StatusKind.Poison, StatusEndReason.Expired), log);
        Assert.False(enemy.has(StatusKind.Poison));
        Assert.Equal(350, enemy.hp);
    }

    [Fact]
    public void poisonCanFinishABattle()
    {
        (GameSession session, BattleEngine engine) = battle("frail");
        cast(engine, "venom");
        engine.enemies[0].hp = 1;

        // The poison tick at the end of the enemy's turn knocks it out; the battle is won during the hero's guard.
        IReadOnlyList<BattleEvent> events = guard(engine);

        Assert.Contains(events, e => e is PoisonDamageEvent);
        Assert.Contains(events, e => e is VictoryEvent);
        Assert.Equal(BattleOutcome.Victory, engine.outcome);
        Assert.Equal(3, session.party[0].exp);
    }

    [Fact]
    public void sleepingCombatantsLoseTheirTurnsAndWakeUpWhenHit()
    {
        (_, BattleEngine engine) = battle("plain");
        Combatant enemy = engine.enemies[0];

        IReadOnlyList<BattleEvent> first = cast(engine, "lull");
        Assert.Contains(new TurnSkippedEvent(enemy, StatusKind.Sleep), first);
        Assert.Equal(Side.Party, engine.currentActor!.side);

        IReadOnlyList<BattleEvent> second = heroRound(engine, new AttackAction(engine.currentActor!, enemy));
        Assert.Contains(new StatusRemovedEvent(enemy, StatusKind.Sleep, StatusEndReason.WokeUp), second);
        Assert.Contains(second, e => e is AttackEvent a && a.actor == enemy);
    }

    [Fact]
    public void sleepWearsOffAfterItsTurns()
    {
        (_, BattleEngine engine) = battle("plain");
        Combatant enemy = engine.enemies[0];

        List<BattleEvent> log = cast(engine, "lull").ToList();
        log.AddRange(guard(engine));
        Assert.Equal(2, log.OfType<TurnSkippedEvent>().Count());
        Assert.Contains(new StatusRemovedEvent(enemy, StatusKind.Sleep, StatusEndReason.Expired), log);

        Assert.Contains(guard(engine), e => e is AttackEvent a && a.actor == enemy);
    }

    [Fact]
    public void immuneTargetsShrugStatusesOff()
    {
        (_, BattleEngine engine) = battle("stone");

        IReadOnlyList<BattleEvent> events = cast(engine, "lull");

        Assert.Contains(new StatusMissedEvent(engine.enemies[0], StatusKind.Sleep), events);
        Assert.Empty(engine.enemies[0].statuses);
    }

    [Fact]
    public void paralysisCostsAboutHalfTheTurns()
    {
        int skipped = 0;
        const int runs = 300;
        for (int seed = 1; seed <= runs; seed++)
        {
            (_, BattleEngine engine) = battle("plain", seed);
            skipped += cast(engine, "numb").OfType<TurnSkippedEvent>().Count(t => t.cause == StatusKind.Paralysis);
        }

        Assert.InRange(skipped, runs * 0.4, runs * 0.6);
    }

    [Fact]
    public void buffsChangeStatsForTheirTurnsAndCancelAgainstDebuffs()
    {
        (_, BattleEngine engine) = battle("plain");
        Combatant hero = engine.party[0];

        cast(engine, "rally");
        Assert.Equal(15, hero.stats.attack);
        Assert.Equal(10, hero.baseStats.attack);

        guard(engine); // the turn it was cast does not count: still up after one more turn
        Assert.True(hero.has(StatusKind.AttackUp));
        IReadOnlyList<BattleEvent> expiry = guard(engine);
        Assert.Contains(new StatusRemovedEvent(hero, StatusKind.AttackUp, StatusEndReason.Expired), expiry);
        Assert.Equal(10, hero.stats.attack);

        cast(engine, "rally");
        IReadOnlyList<BattleEvent> cancel = cast(engine, "sap");
        Assert.Contains(new StatusRemovedEvent(hero, StatusKind.AttackUp, StatusEndReason.Cancelled), cancel);
        Assert.Empty(hero.statuses);
    }

    [Fact]
    public void curesRemoveStatusesBeforeTheyTick()
    {
        (GameSession session, BattleEngine engine) = battle("viper");
        session.inventory.add("antidote");
        Combatant hero = engine.party[0];

        engine.execute(engine.decideEnemyAction()); // the viper is faster
        Assert.True(hero.has(StatusKind.Poison));

        IReadOnlyList<BattleEvent> events = engine.execute(new ItemAction(hero, session.content.item("antidote"), hero));

        Assert.Contains(new StatusRemovedEvent(hero, StatusKind.Poison, StatusEndReason.Cured), events);
        Assert.DoesNotContain(events, e => e is PoisonDamageEvent);
        Assert.Equal(0, session.inventory.count("antidote"));
    }

    [Fact]
    public void statusesDoNotOutliveTheBattle()
    {
        (GameSession session, BattleEngine engine) = battle("viper");
        engine.execute(engine.decideEnemyAction());
        Assert.True(engine.party[0].has(StatusKind.Poison));

        BattleEngine next = new(session, [session.content.enemy("plain")]);
        Assert.Empty(next.party[0].statuses);
    }

    [Fact]
    public void enemiesDoNotRecastABuffTheyAlreadyHave()
    {
        (_, BattleEngine engine) = battle("brute");

        Assert.IsType<SkillAction>(engine.decideEnemyAction());
        engine.execute(engine.decideEnemyAction());
        engine.execute(new GuardAction(engine.currentActor!));

        Assert.IsType<AttackAction>(engine.decideEnemyAction());
    }

    [Fact]
    public void invalidStatusEffectsAreReported()
    {
        Dictionary<string, string> broken = files();
        broken[ContentDb.skillsFile] = """
            [
              { "id": "nothing", "name": "N", "target": "Self", "effect": { "kind": "Status" } },
              { "id": "hotHeal", "name": "H", "target": "Self", "effect": { "kind": "Heal", "power": 5, "element": "Fire" } },
              { "id": "never", "name": "V", "target": "SingleEnemy", "effect": { "kind": "Status", "status": { "kind": "Sleep", "chance": 0 } } }
            ]
            """;
        broken[ContentDb.charactersFile] = "[]";
        broken[ContentDb.enemiesFile] = "[]";

        ContentException ex = Assert.Throws<ContentException>(() => ContentDb.parse(broken));

        Assert.Contains(ex.errors, e => e.Contains("'nothing'") && e.Contains("needs 'status' or 'cures'"));
        Assert.Contains(ex.errors, e => e.Contains("'hotHeal'") && e.Contains("element"));
        Assert.Contains(ex.errors, e => e.Contains("'never'") && e.Contains("chance"));
    }
}
