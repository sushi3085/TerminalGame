namespace TerminalGame.Rpg.Data;

/// <summary>
/// The six numbers every combatant is built from. Used for base stats, per-level growth and equipment
/// bonuses alike, so they add up with <c>+</c>. Missing fields in JSON default to 0.
/// </summary>
public readonly record struct StatBlock
{
    public int maxHp { get; init; }
    public int maxMp { get; init; }
    public int attack { get; init; }
    public int defense { get; init; }
    public int magic { get; init; }
    public int speed { get; init; }

    public static StatBlock operator +(StatBlock a, StatBlock b) => new()
    {
        maxHp = a.maxHp + b.maxHp,
        maxMp = a.maxMp + b.maxMp,
        attack = a.attack + b.attack,
        defense = a.defense + b.defense,
        magic = a.magic + b.magic,
        speed = a.speed + b.speed,
    };

    public static StatBlock operator *(StatBlock a, int factor) => new()
    {
        maxHp = a.maxHp * factor,
        maxMp = a.maxMp * factor,
        attack = a.attack * factor,
        defense = a.defense * factor,
        magic = a.magic * factor,
        speed = a.speed * factor,
    };

    /// <summary>Keeps final stats sane when penalties exceed bonuses: maxHp ≥ 1, everything else ≥ 0.</summary>
    public StatBlock clamped() => new()
    {
        maxHp = Math.Max(1, maxHp),
        maxMp = Math.Max(0, maxMp),
        attack = Math.Max(0, attack),
        defense = Math.Max(0, defense),
        magic = Math.Max(0, magic),
        speed = Math.Max(0, speed),
    };
}
