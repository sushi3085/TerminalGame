using TerminalGame.Rpg.Data;

namespace TerminalGame.Rpg.Battle;

/// <summary>Numbers and small rules for statuses and elements, next to <see cref="DamageFormula"/>.</summary>
public static class StatusRules
{
    public const double weakMultiplier = 1.5;
    public const double resistMultiplier = 0.5;
    public const double buffMultiplier = 1.5;
    public const double debuffMultiplier = 0.75;

    /// <summary>Share of max HP lost to poison at the end of each of the holder's turns (at least 1).</summary>
    public const double poisonShare = 0.1;

    /// <summary>Chance that paralysis costs a turn.</summary>
    public const double paralysisChance = 0.5;

    /// <summary>The buff a debuff cancels and vice versa; applying one while the other is active just removes both.</summary>
    public static StatusKind? opposite(StatusKind kind) => kind switch
    {
        StatusKind.AttackUp => StatusKind.AttackDown,
        StatusKind.AttackDown => StatusKind.AttackUp,
        StatusKind.DefenseUp => StatusKind.DefenseDown,
        StatusKind.DefenseDown => StatusKind.DefenseUp,
        _ => null,
    };

    public static int poisonDamage(StatBlock stats) => Math.Max(1, (int)(stats.maxHp * poisonShare));

    public static StatBlock modify(StatBlock stats, IReadOnlyList<ActiveStatus> statuses)
    {
        if (statuses.Count == 0)
        {
            return stats;
        }

        double attack = 1;
        double defense = 1;
        foreach (ActiveStatus status in statuses)
        {
            switch (status.kind)
            {
                case StatusKind.AttackUp: attack *= buffMultiplier; break;
                case StatusKind.AttackDown: attack *= debuffMultiplier; break;
                case StatusKind.DefenseUp: defense *= buffMultiplier; break;
                case StatusKind.DefenseDown: defense *= debuffMultiplier; break;
            }
        }

        return stats with
        {
            attack = (int)Math.Round(stats.attack * attack),
            defense = (int)Math.Round(stats.defense * defense),
        };
    }

    public static Effectiveness effectivenessOf(double elementRate) =>
        elementRate > 1 ? Effectiveness.Weak : elementRate < 1 ? Effectiveness.Resisted : Effectiveness.Normal;
}
