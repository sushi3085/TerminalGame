using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;

namespace TerminalGame.Sim;

public sealed record MemberSetup(string characterId, int level, params string[] gear);

/// <summary>A point in the story: the party the designer expects the player to have when they get here.</summary>
public sealed record Checkpoint(string name, IReadOnlyList<MemberSetup> party, IReadOnlyList<(string itemId, int count)> items)
{
    public GameSession build(ContentDb content, int seed)
    {
        GameSession session = new(content, seed);
        foreach (MemberSetup setup in party)
        {
            PartyMember member = session.addMember(setup.characterId, setup.level);
            foreach (string itemId in setup.gear)
            {
                member.equip(content.item(itemId));
            }

            member.restore();
        }

        foreach ((string itemId, int count) in items)
        {
            session.inventory.add(itemId, count);
        }

        return session;
    }
}

public sealed record BattleResult(BattleOutcome outcome, int rounds, double hpLeft, int mpUsed, int exp, int itemsUsed);

public sealed record Summary(int runs, double winRate, double hpLeft, double rounds, double mpUsed, double itemsUsed)
{
    public static Summary of(IReadOnlyList<BattleResult> results)
    {
        List<BattleResult> wins = results.Where(r => r.outcome == BattleOutcome.Victory).ToList();
        return new Summary(
            results.Count,
            wins.Count / (double)results.Count,
            wins.Count == 0 ? 0 : wins.Average(r => r.hpLeft),
            results.Average(r => r.rounds),
            results.Average(r => r.mpUsed),
            results.Average(r => r.itemsUsed));
    }
}

public static class Simulator
{
    public const int maxRounds = 60;

    /// <summary>Plays one battle with <paramref name="policy"/> for the party; the session's HP/MP/items carry the result.</summary>
    public static BattleResult fight(GameSession session, IEnumerable<string> enemyIds, AutoPolicy policy)
    {
        int mpBefore = session.party.Sum(m => m.mp);
        int itemsUsed = 0;
        int exp = 0;
        BattleEngine engine = new(session, enemyIds.Select(session.content.enemy));
        engine.start();
        while (engine.outcome == BattleOutcome.Ongoing && engine.round <= maxRounds)
        {
            BattleAction action = engine.currentActor!.side == Side.Party ? policy.decide(engine, session) : engine.decideEnemyAction();
            IReadOnlyList<BattleEvent> events = engine.execute(action);
            itemsUsed += events.OfType<ItemUsedEvent>().Count();
            exp += events.OfType<VictoryEvent>().Sum(v => v.exp);
        }

        int maxHp = session.party.Sum(m => m.stats.maxHp);
        return new BattleResult(
            engine.outcome,
            engine.round,
            session.party.Sum(m => m.hp) / (double)maxHp,
            mpBefore - session.party.Sum(m => m.mp),
            exp,
            itemsUsed);
    }

    /// <summary>The same fight from a fresh checkpoint party, <paramref name="runs"/> times.</summary>
    public static Summary repeat(ContentDb content, Checkpoint checkpoint, IReadOnlyList<string> enemyIds, AutoPolicy policy, int runs, int seed = 1)
    {
        List<BattleResult> results = new();
        for (int i = 0; i < runs; i++)
        {
            GameSession session = checkpoint.build(content, seed + i);
            results.Add(fight(session, enemyIds, policy));
        }

        return Summary.of(results);
    }

    /// <summary>
    /// How many random battles in a row the party survives before it should head back to rest. Between battles the party
    /// heals with magic from the field menu; a sensible player turns back once someone falls, or total HP is below
    /// <paramref name="retreatAt"/> with no healing magic left (potions are kept for emergencies). Returns the average battles per trip, the share of trips that end in defeat, and exp.
    /// </summary>
    public static (double battles, double defeatRate, double expPerTrip) trips(
        ContentDb content, Checkpoint checkpoint, LocationDef location, AutoPolicy policy, int runs, double retreatAt = 0.5, int seed = 1)
    {
        double battles = 0;
        int defeats = 0;
        double exp = 0;
        for (int i = 0; i < runs; i++)
        {
            GameSession session = checkpoint.build(content, seed + i);
            for (int n = 0; n < 50; n++)
            {
                EncounterDef encounter = pick(location, session.random);
                BattleResult result = fight(session, encounter.enemies, policy);
                if (result.outcome != BattleOutcome.Victory)
                {
                    defeats++;
                    break;
                }

                battles++;
                exp += result.exp;
                healBetweenBattles(session);
                bool canHeal = session.party.Any(m => m.isAlive && m.skills.Any(s => s.effect.kind == EffectKind.Heal && m.mp >= s.mpCost));
                double hpLeft = session.party.Sum(m => m.hp) / (double)session.party.Sum(m => m.stats.maxHp);
                if (session.party.Any(m => !m.isAlive) || (hpLeft < retreatAt && !canHeal))
                {
                    break;
                }
            }
        }

        return (battles / runs, defeats / (double)runs, exp / runs);
    }

    /// <summary>What a player does from the field menu after a fight: heal anyone below 60% with healing magic while MP lasts.</summary>
    private static void healBetweenBattles(GameSession session)
    {
        for (int guard = 0; guard < 20; guard++)
        {
            PartyMember? hurt = session.party.Where(m => m.isAlive && m.hp < m.stats.maxHp * 0.6).OrderBy(m => (double)m.hp / m.stats.maxHp).FirstOrDefault();
            if (hurt is null)
            {
                return;
            }

            (PartyMember caster, SkillDef skill)? cast = session.party
                .Where(m => m.isAlive)
                .SelectMany(m => m.skills.Where(s => s.effect.kind == EffectKind.Heal && FieldRules.usableInField(s) && m.mp >= s.mpCost).Select(s => (m, s)))
                .OrderBy(c => c.s.mpCost)
                .Cast<(PartyMember, SkillDef)?>()
                .FirstOrDefault();
            if (cast is not (PartyMember caster, SkillDef skill))
            {
                return;
            }

            FieldRules.castSkill(session, caster, skill, hurt);
        }
    }

    private static EncounterDef pick(LocationDef location, Random random)
    {
        int roll = random.Next(location.encounters.Sum(e => e.weight));
        foreach (EncounterDef encounter in location.encounters)
        {
            if (roll < encounter.weight)
            {
                return encounter;
            }

            roll -= encounter.weight;
        }

        return location.encounters[^1];
    }
}
