namespace TerminalGame.Rpg.Data;

// The world is a graph of locations (content/locations.json). Each location shows its art and description,
// and offers a menu of exits (to other locations) and spots (people and things that run a script).
// Conditions are strings in the TerminalGame.Rpg.Script.Condition syntax.

public enum LocationKind
{
    /// <summary>Safe: no random encounters.</summary>
    Town,

    Field,
    Dungeon,
}

public sealed record ExitDef
{
    public required string to { get; init; }

    /// <summary>Menu text; empty = "前往" + the destination's name.</summary>
    public string label { get; init; } = "";

    /// <summary>Shown only while this holds.</summary>
    public string? @if { get; init; }
}

public sealed record SpotDef
{
    public required string label { get; init; }
    public required string script { get; init; }
    public string? @if { get; init; }
}

public sealed record EncounterDef
{
    public required IReadOnlyList<string> enemies { get; init; }
    public int weight { get; init; } = 1;
}

/// <summary>Runs <see cref="script"/> on arrival when <see cref="if"/> holds. Only the first matching trigger runs.</summary>
public sealed record TriggerDef
{
    public string? @if { get; init; }
    public required string script { get; init; }
}

public sealed record LocationDef
{
    public required string id { get; init; }
    public required string name { get; init; }
    public LocationKind kind { get; init; } = LocationKind.Field;

    /// <summary>Shown in the message window on arrival.</summary>
    public string description { get; init; } = "";

    public IReadOnlyList<string> art { get; init; } = [];

    /// <summary>Name of a color theme the UI applies here (forest, town, cave…). Empty = default.</summary>
    public string palette { get; init; } = "";

    public IReadOnlyList<ExitDef> exits { get; init; } = [];
    public IReadOnlyList<SpotDef> spots { get; init; } = [];
    public IReadOnlyList<TriggerDef> onEnter { get; init; } = [];

    /// <summary>Chance of a random battle when arriving here or searching (0–1). Towns never have encounters.</summary>
    public double encounterRate { get; init; }

    public IReadOnlyList<EncounterDef> encounters { get; init; } = [];

    public bool hasEncounters => kind != LocationKind.Town && encounters.Count > 0;
}

/// <summary>One line of the adventure journal, shown while <see cref="if"/> holds.</summary>
public sealed record JournalEntryDef
{
    public string? @if { get; init; }
    public required string text { get; init; }
}

/// <summary>
/// content/journal.json. Everything is derived from flags, so the journal needs nothing in the save file.
/// <see cref="objectives"/> are listed in story order and the <i>last</i> one whose condition holds is the current
/// goal; every <see cref="chronicle"/> entry whose condition holds is shown, in order.
/// </summary>
public sealed record JournalDef
{
    public IReadOnlyList<JournalEntryDef> objectives { get; init; } = [];
    public IReadOnlyList<JournalEntryDef> chronicle { get; init; } = [];
}

/// <summary>
/// A party conversation (content/banter.json), offered as "隊伍閒聊" wherever <see cref="at"/> allows while
/// <see cref="if"/> holds. One-off banters are remembered with the flag <c>banter_&lt;id&gt;</c>; repeatable ones
/// (<see cref="once"/> false) fill in when nothing new is left to say.
/// </summary>
public sealed record BanterDef
{
    public required string id { get; init; }
    public string? @if { get; init; }

    /// <summary>Location ids; empty = anywhere.</summary>
    public IReadOnlyList<string> at { get; init; } = [];

    public required string script { get; init; }
    public bool once { get; init; } = true;
}

public sealed record ShopDef
{
    public required string id { get; init; }
    public required string name { get; init; }
    public required IReadOnlyList<string> items { get; init; }
}

/// <summary>
/// One step of an event script (content/scripts.json maps script id → list of commands). Exactly one operation
/// field is set per command; the others hold its arguments.
/// <code>
/// { "say": "text", "speaker": "長老" }              { "if": "flag:x == 0", "then": [...], "else": [...] }
/// { "choice": [ { "label": "是", "then": [...] } ] }  { "setFlag": "x", "value": 2 }   { "addFlag": "x", "value": 1 }
/// { "giveItem": "potion", "count": 2 }               { "takeItem": "key" }           { "giveGold": 50 }  { "takeGold": 10 }
/// { "join": "rin", "level": 4 }                      { "battle": ["boss"], "canEscape": false }
/// { "shop": "shopId" }   { "inn": 10 }   { "travel": "locationId" }   { "run": "otherScript" }
/// { "restore": true }    { "end": true }
/// { "cases": [ { "if": "flag:x", "then": [...] }, { "then": [...] } ] }   first case whose condition holds
/// { "oneOf": [ { "if": "flag:x", "then": [...] }, { "then": [...] } ] }   a random case among those that hold
/// </code>
/// </summary>
public sealed record ScriptCommandDef
{
    public string? say { get; init; }
    public string? speaker { get; init; }

    public string? @if { get; init; }
    public IReadOnlyList<ScriptCommandDef>? then { get; init; }
    public IReadOnlyList<ScriptCommandDef>? @else { get; init; }

    public IReadOnlyList<ChoiceDef>? choice { get; init; }

    public IReadOnlyList<CaseDef>? cases { get; init; }
    public IReadOnlyList<CaseDef>? oneOf { get; init; }

    public string? setFlag { get; init; }
    public string? addFlag { get; init; }
    public int value { get; init; } = 1;

    public string? giveItem { get; init; }
    public string? takeItem { get; init; }
    public int count { get; init; } = 1;

    public int? giveGold { get; init; }
    public int? takeGold { get; init; }

    public string? join { get; init; }

    /// <summary>For <see cref="join"/>: the minimum level; the newcomer joins at the party's average level if that is higher.</summary>
    public int level { get; init; } = 1;

    public IReadOnlyList<string>? battle { get; init; }
    public bool canEscape { get; init; } = true;

    public string? shop { get; init; }
    public int? inn { get; init; }
    public string? travel { get; init; }
    public string? run { get; init; }
    public bool restore { get; init; }
    public bool end { get; init; }

    /// <summary>Names of the operation fields that are set; valid commands have exactly one.</summary>
    public IEnumerable<string> operations
    {
        get
        {
            if (say is not null) yield return "say";
            if (@if is not null) yield return "if";
            if (choice is not null) yield return "choice";
            if (cases is not null) yield return "cases";
            if (oneOf is not null) yield return "oneOf";
            if (setFlag is not null) yield return "setFlag";
            if (addFlag is not null) yield return "addFlag";
            if (giveItem is not null) yield return "giveItem";
            if (takeItem is not null) yield return "takeItem";
            if (giveGold is not null) yield return "giveGold";
            if (takeGold is not null) yield return "takeGold";
            if (join is not null) yield return "join";
            if (battle is not null) yield return "battle";
            if (shop is not null) yield return "shop";
            if (inn is not null) yield return "inn";
            if (travel is not null) yield return "travel";
            if (run is not null) yield return "run";
            if (restore) yield return "restore";
            if (end) yield return "end";
        }
    }
}

public sealed record ChoiceDef
{
    public required string label { get; init; }

    /// <summary>The option is hidden unless this holds.</summary>
    public string? @if { get; init; }

    public IReadOnlyList<ScriptCommandDef> then { get; init; } = [];
}

/// <summary>A branch of <c>cases</c> / <c>oneOf</c>; no condition = always eligible.</summary>
public sealed record CaseDef
{
    public string? @if { get; init; }
    public IReadOnlyList<ScriptCommandDef> then { get; init; } = [];
}
