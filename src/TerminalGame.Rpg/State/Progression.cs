namespace TerminalGame.Rpg.State;

/// <summary>Experience curve: <c>expToNext(level) = round(scale · level^exponent)</c>.</summary>
public static class Progression
{
    public const int maxLevel = 30;
    public const double scale = 20;
    public const double exponent = 1.6;

    /// <summary>Experience needed to go from <paramref name="level"/> to the next one; 0 at the cap.</summary>
    public static int expToNext(int level) =>
        level >= maxLevel ? 0 : (int)Math.Round(scale * Math.Pow(level, exponent));
}
