using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Battle;

/// <summary>
/// A reasonable player, for the balance simulator (and a future in-game "auto" command). Priorities each turn:
/// <list type="number">
/// <item>Heal an ally below <see cref="healThreshold"/> with a skill, else a potion.</item>
/// <item>Wake a sleeping ally or cure paralysis/poison, with a skill or an item.</item>
/// <item>In a long fight (when <see cref="spendMp"/> is on), buff the strongest ally that lacks it.</item>
/// <item>Hit two or more enemies with an area skill.</item>
/// <item>When <see cref="spendMp"/> is on, use the strongest affordable single-target skill.</item>
/// <item>Otherwise attack the enemy with the least HP left (finish things off).</item>
/// </list>
/// MP is kept for healing: damage skills are only used while MP stays above the cost of one heal.
/// </summary>
public sealed class AutoPolicy
{
    public double healThreshold { get; init; } = 0.4;

    /// <summary>Use damage skills freely (boss fights); off = save MP across a string of random battles.</summary>
    public bool spendMp { get; init; } = true;

    /// <summary>Use consumables (potions) when no healing skill is available.</summary>
    public bool useItems { get; init; } = true;

    public BattleAction decide(BattleEngine engine, GameSession session)
    {
        Combatant actor = engine.currentActor ?? throw new InvalidOperationException("no one is acting");
        IReadOnlyList<Combatant> allies = engine.alliesOf(actor);
        IReadOnlyList<Combatant> enemies = engine.opponentsOf(actor);
        IReadOnlyList<SkillDef> skills = engine.skillsOf(actor).Where(s => engine.canAfford(actor, s)).ToList();

        Combatant? hurt = allies
            .Where(a => (double)a.hp / a.stats.maxHp < healThreshold)
            .OrderBy(a => (double)a.hp / a.stats.maxHp)
            .FirstOrDefault();
        if (hurt is not null)
        {
            SkillDef? heal = skills
                .Where(s => s.effect.kind == EffectKind.Heal && s.target is TargetKind.SingleAlly or TargetKind.AllAllies)
                .OrderByDescending(s => s.effect.power)
                .FirstOrDefault();
            if (heal is not null)
            {
                return new SkillAction(actor, heal, hurt);
            }

            ItemDef? potion = useItems ? bestHealingItem(session) : null;
            if (potion is not null)
            {
                return new ItemAction(actor, potion, hurt);
            }
        }

        if (cureAction(session, actor, allies, skills) is { } cure)
        {
            return cure;
        }

        if (spendMp && buffAction(actor, allies, enemies, skills) is { } buff)
        {
            return buff;
        }

        int healReserve = engine.skillsOf(actor).Where(s => s.effect.kind == EffectKind.Heal).Select(s => s.mpCost).DefaultIfEmpty(0).Min();
        bool canSpend(SkillDef s) => actor.mp - s.mpCost >= healReserve || healReserve == 0;
        bool isDamage(SkillDef s) => s.effect.kind is EffectKind.Physical or EffectKind.Magical;

        if (enemies.Count >= 2)
        {
            SkillDef? area = skills.Where(s => isDamage(s) && s.target == TargetKind.AllEnemies && canSpend(s))
                .OrderByDescending(s => s.effect.power)
                .FirstOrDefault();
            if (area is not null && (spendMp || enemies.Count >= 3))
            {
                return new SkillAction(actor, area, null);
            }
        }

        Combatant target = enemies.OrderBy(e => e.hp).ThenBy(e => e.slot).First();
        if (spendMp)
        {
            SkillDef? strongest = skills.Where(s => isDamage(s) && s.target == TargetKind.SingleEnemy && canSpend(s))
                .OrderByDescending(s => expectedDamage(actor, target, s.effect))
                .FirstOrDefault();
            if (strongest is not null && expectedDamage(actor, target, strongest.effect) > expectedDamage(actor, target, attack))
            {
                return new SkillAction(actor, strongest, target);
            }
        }

        return new AttackAction(actor, target);
    }

    private static readonly EffectDef attack = new() { kind = EffectKind.Physical, power = 100 };

    /// <summary>Worst first: a sleeping ally loses every turn, a paralysed one half, poison only chips away.</summary>
    private static readonly StatusKind[] curable = [StatusKind.Sleep, StatusKind.Paralysis, StatusKind.Poison];

    private BattleAction? cureAction(GameSession session, Combatant actor, IReadOnlyList<Combatant> allies, IReadOnlyList<SkillDef> skills)
    {
        foreach (StatusKind kind in curable)
        {
            Combatant? patient = allies.FirstOrDefault(a => a.has(kind));
            if (patient is null)
            {
                continue;
            }

            SkillDef? skill = skills.FirstOrDefault(s => s.effect.cures.Contains(kind) && s.target is TargetKind.SingleAlly or TargetKind.AllAllies);
            if (skill is not null)
            {
                return new SkillAction(actor, skill, patient);
            }

            ItemDef? item = useItems
                ? session.inventory.entries.Select(e => session.content.item(e.itemId))
                    .FirstOrDefault(i => i.usableInBattle && i.effect!.cures.Contains(kind))
                : null;
            if (item is not null)
            {
                return new ItemAction(actor, item, patient);
            }
        }

        return null;
    }

    /// <summary>Worth a turn only while the enemies have plenty of HP left compared to what the actor deals per hit.</summary>
    private static BattleAction? buffAction(
        Combatant actor, IReadOnlyList<Combatant> allies, IReadOnlyList<Combatant> enemies, IReadOnlyList<SkillDef> skills)
    {
        double perHit = enemies.Average(e => expectedDamage(actor, e, attack));
        if (enemies.Sum(e => e.hp) < perHit * 6)
        {
            return null;
        }

        foreach (SkillDef skill in skills.Where(s => s.effect is { kind: EffectKind.Status, status.kind: StatusKind.AttackUp or StatusKind.DefenseUp }))
        {
            StatusKind kind = skill.effect.status!.kind;
            Combatant? target = skill.target switch
            {
                TargetKind.Self => actor.has(kind) ? null : actor,
                TargetKind.SingleAlly => allies.Where(a => !a.has(kind)).OrderByDescending(a => kind == StatusKind.AttackUp ? a.stats.attack : a.stats.defense).FirstOrDefault(),
                TargetKind.AllAllies => allies.Any(a => !a.has(kind)) ? actor : null,
                _ => null,
            };
            if (target is not null)
            {
                return new SkillAction(actor, skill, target);
            }
        }

        return null;
    }

    private static double expectedDamage(Combatant user, Combatant target, EffectDef effect) =>
        Math.Max(1, DamageFormula.baseDamage(user.stats, target.stats, effect) * target.elementRate(effect.element));

    private static ItemDef? bestHealingItem(GameSession session) =>
        session.inventory.entries
            .Select(e => session.content.item(e.itemId))
            .Where(i => i.usableInBattle && i.effect!.kind == EffectKind.Heal)
            .OrderBy(i => i.effect!.power)
            .FirstOrDefault();
}
