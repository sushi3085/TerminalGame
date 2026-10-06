using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.Script;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.World;

/// <summary>The adventure journal: what to do next and what has happened so far, both read off the flags.</summary>
public static class JournalRules
{
    /// <summary>The last objective (in story order) whose condition holds; null when none does.</summary>
    public static string? currentObjective(this GameSession session) =>
        session.content.journal.objectives.LastOrDefault(o => Condition.check(o.@if, session))?.text;

    public static IReadOnlyList<string> chronicle(this GameSession session) =>
        session.content.journal.chronicle.Where(e => Condition.check(e.@if, session)).Select(e => e.text).ToList();
}

/// <summary>Picks what the party talks about when the player chooses "隊伍閒聊".</summary>
public static class BanterRules
{
    public const string seenPrefix = "banter_";

    /// <summary>Banters needing a companion are pointless alone, so the option only exists with two or more members.</summary>
    public static IReadOnlyList<BanterDef> availableBanters(this GameSession session, LocationDef location) =>
        session.party.Count < 2
            ? []
            : session.content.banters
                .Where(b => b.at.Count == 0 || b.at.Contains(location.id))
                .Where(b => !b.once || !session.flags.isSet(seenPrefix + b.id))
                .Where(b => Condition.check(b.@if, session))
                .ToList();

    /// <summary>True when there is something the party has not talked about yet (the menu marks it).</summary>
    public static bool hasNewBanter(this GameSession session, LocationDef location) =>
        session.availableBanters(location).Any(b => b.once);

    /// <summary>The first unseen one-off banter, else a random repeatable one, else null. Marks a one-off as seen.</summary>
    public static BanterDef? takeBanter(this GameSession session, LocationDef location)
    {
        IReadOnlyList<BanterDef> available = session.availableBanters(location);
        BanterDef? banter = available.FirstOrDefault(b => b.once);
        if (banter is not null)
        {
            session.flags[seenPrefix + banter.id] = 1;
            return banter;
        }

        List<BanterDef> repeatable = available.Where(b => !b.once).ToList();
        return repeatable.Count == 0 ? null : repeatable[session.random.Next(repeatable.Count)];
    }
}
