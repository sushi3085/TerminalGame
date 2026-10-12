namespace TerminalGame.Rpg.State;

/// <summary>
/// Experience curve: <c>expToNext(level) = round(scale · level^exponent)</c>, plus the level-gap rules that keep
/// over-levelled parties from snowballing: experience and random encounters taper off once a member is more than
/// <see cref="graceLevels"/> above the enemies. Gold is never scaled, so extra grinding turns into money for gear.
/// </summary>
public static class Progression
{
    public const int maxLevel = 30;
    public const double scale = 20;
    public const double exponent = 1.6;

    /// <summary>How many levels above an enemy a member can be before its experience starts to drop.</summary>
    public const int graceLevels = 3;

    /// <summary>Experience lost per level beyond the grace window, down to <see cref="minExpRate"/>.</summary>
    public const double expFalloffPerLevel = 0.2;

    public const double minExpRate = 0.2;

    /// <summary>Encounter rate lost per level beyond the grace window, down to <see cref="minEncounterRate"/>.</summary>
    public const double encounterFalloffPerLevel = 0.25;

    public const double minEncounterRate = 0.25;

    /// <summary>Experience needed to go from <paramref name="level"/> to the next one; 0 at the cap.</summary>
    public static int expToNext(int level) =>
        level >= maxLevel ? 0 : (int)Math.Round(scale * Math.Pow(level, exponent));

    /// <summary>Share (0.2–1) of an enemy's experience a member of <paramref name="memberLevel"/> receives.</summary>
    public static double expRate(int memberLevel, int enemyLevel) =>
        falloff(memberLevel - enemyLevel, expFalloffPerLevel, minExpRate);

    /// <summary>Experience a member of <paramref name="memberLevel"/> earns from one enemy worth <paramref name="baseExp"/>.</summary>
    public static int scaledExp(int baseExp, int memberLevel, int enemyLevel) =>
        (int)Math.Round(baseExp * expRate(memberLevel, enemyLevel));

    /// <summary>Multiplier (0.25–1) on an area's encounter rate for a party of <paramref name="partyLevel"/>.</summary>
    public static double encounterScale(double partyLevel, int areaLevel) =>
        falloff(partyLevel - areaLevel, encounterFalloffPerLevel, minEncounterRate);

    private static double falloff(double levelGap, double perLevel, double floor) =>
        Math.Clamp(1 - perLevel * (levelGap - graceLevels), floor, 1);
}
