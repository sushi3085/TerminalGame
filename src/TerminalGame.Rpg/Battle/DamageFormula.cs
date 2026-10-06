using TerminalGame.Rpg.Data;

namespace TerminalGame.Rpg.Battle;

/// <summary>
/// All battle arithmetic in one place so balance changes (and the simulator) touch a single file.
/// <code>
/// physical = attack × power% − defense / 2
/// magical  = magic  × power% − defense / 4
/// damage   = max(1, round(base × U(1 − variance, 1 + variance) × element))   critical: base × 1.5 (physical only)
/// heal     = power + magic × magicScaling%                          (± healVariance when it scales)
/// </code>
/// </summary>
public static class DamageFormula
{
    public const double variance = 0.15;
    public const double healVariance = 0.1;
    public const double criticalChance = 1.0 / 16;
    public const double criticalMultiplier = 1.5;
    public const double guardMultiplier = 0.5;

    /// <summary>Damage before randomness, criticals and guarding; may be ≤ 0.</summary>
    public static double baseDamage(StatBlock user, StatBlock target, EffectDef effect) => effect.kind switch
    {
        EffectKind.Physical => user.attack * effect.power / 100.0 - target.defense / 2.0,
        EffectKind.Magical => user.magic * effect.power / 100.0 - target.defense / 4.0,
        _ => throw new ArgumentException($"{effect.kind} is not a damage effect", nameof(effect)),
    };

    /// <param name="elementRate">From <see cref="Combatant.elementRate"/>: 1.5 weak, 0.5 resisted.</param>
    public static (int amount, bool isCritical) rollDamage(
        StatBlock user, StatBlock target, EffectDef effect, bool targetGuarding, Random random, double elementRate = 1)
    {
        double value = baseDamage(user, target, effect);
        bool isCritical = effect.kind == EffectKind.Physical && random.NextDouble() < criticalChance;
        if (isCritical)
        {
            value *= criticalMultiplier;
        }

        value *= spread(random, variance) * elementRate;
        if (targetGuarding)
        {
            value *= guardMultiplier;
        }

        return (Math.Max(1, (int)Math.Round(value)), isCritical);
    }

    public static int rollHeal(StatBlock user, EffectDef effect, Random random)
    {
        double value = effect.power + user.magic * effect.magicScaling / 100.0;
        if (effect.magicScaling != 0)
        {
            value *= spread(random, healVariance);
        }

        return Math.Max(0, (int)Math.Round(value));
    }

    /// <summary>Chance that the party escapes, from the average speed of each side's living members.</summary>
    public static double escapeChance(double partySpeed, double enemySpeed) =>
        Math.Clamp(0.5 + 0.05 * (partySpeed - enemySpeed), 0.25, 0.95);

    private static double spread(Random random, double amount) => 1 + (random.NextDouble() * 2 - 1) * amount;
}
