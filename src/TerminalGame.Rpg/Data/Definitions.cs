namespace TerminalGame.Rpg.Data;

// Read-only definitions loaded from content/*.json. They are shared by the whole game and never mutated;
// everything that changes during play lives in TerminalGame.Rpg.State and refers to these by id.

public enum ItemKind
{
    Consumable,
    Equipment,
    Key,
}

public enum EquipSlot
{
    Weapon,
    Body,
    Head,
    Accessory,
}

public enum TargetKind
{
    Self,
    SingleAlly,
    AllAllies,
    SingleEnemy,
    AllEnemies,
}

public enum EffectKind
{
    /// <summary>attack × power% − defense/2.</summary>
    Physical,

    /// <summary>magic × power% − defense/4.</summary>
    Magical,

    /// <summary>Restores power + magic × magicScaling% HP.</summary>
    Heal,

    /// <summary>Restores power MP.</summary>
    RestoreMp,

    /// <summary>No HP/MP change: only applies <see cref="EffectDef.status"/> and/or removes <see cref="EffectDef.cures"/>.</summary>
    Status,

    /// <summary>Brings a fallen ally back with power% of max HP (at least 1). Only fallen allies can be targeted.</summary>
    Revive,
}

/// <summary>Damage element. Enemies list what they are weak to (×1.5) or resist (×0.5).</summary>
public enum Element
{
    None,
    Fire,
    Ice,
    Thunder,
    Holy,
}

/// <summary>Battle-only conditions; all of them wear off when the battle ends.</summary>
public enum StatusKind
{
    /// <summary>Loses 1/10 of max HP at the end of each of its turns.</summary>
    Poison,

    /// <summary>Cannot act; taking damage wakes it up.</summary>
    Sleep,

    /// <summary>Half of its turns are lost.</summary>
    Paralysis,

    AttackUp,
    DefenseUp,
    AttackDown,
    DefenseDown,

    /// <summary>Draws the other side's single-target attacks and skills.</summary>
    Taunt,
}

/// <summary>A status an effect may inflict on each target it hits.</summary>
public sealed record StatusEffectDef
{
    public required StatusKind kind { get; init; }

    /// <summary>0–1. Rolled per target.</summary>
    public double chance { get; init; } = 1;

    /// <summary>How many of the target's own turns it lasts (counted at the end of each of its turns).</summary>
    public int turns { get; init; } = 3;
}

/// <summary>What a skill or a consumable does to each of its targets.</summary>
public sealed record EffectDef
{
    public required EffectKind kind { get; init; }

    /// <summary>Damage: percentage of the user's attack/magic. Heal/RestoreMp: flat amount.</summary>
    public int power { get; init; }

    /// <summary>Heal only: extra percentage of the user's magic added to <see cref="power"/>.</summary>
    public int magicScaling { get; init; }

    /// <summary>Damage only.</summary>
    public Element element { get; init; } = Element.None;

    /// <summary>Inflicted on each target that is still standing afterwards (and, for damage, was actually hit).</summary>
    public StatusEffectDef? status { get; init; }

    /// <summary>Statuses removed from each target.</summary>
    public IReadOnlyList<StatusKind> cures { get; init; } = [];
}

public sealed record SkillDef
{
    public required string id { get; init; }
    public required string name { get; init; }
    public int mpCost { get; init; }
    public required TargetKind target { get; init; }
    public required EffectDef effect { get; init; }
    public string description { get; init; } = "";

    /// <summary>Battle narration after the user's name, e.g. "詠唱[magenta]火球術[/]！". Empty = a generic line.</summary>
    public string useMessage { get; init; } = "";
}

public sealed record ItemDef
{
    public required string id { get; init; }
    public required string name { get; init; }
    public required ItemKind kind { get; init; }
    public int price { get; init; }
    public string description { get; init; } = "";

    /// <summary>Equipment only.</summary>
    public EquipSlot? slot { get; init; }

    /// <summary>Equipment only: added to the wearer's stats.</summary>
    public StatBlock bonus { get; init; }

    /// <summary>Equipment only: character ids that can wear it; empty = anyone.</summary>
    public IReadOnlyList<string> equippableBy { get; init; } = [];

    public bool canBeEquippedBy(CharacterDef character) => equippableBy.Count == 0 || equippableBy.Contains(character.id);

    /// <summary>Consumables only.</summary>
    public TargetKind target { get; init; } = TargetKind.SingleAlly;

    /// <summary>Consumables only.</summary>
    public EffectDef? effect { get; init; }

    public bool usableInBattle => kind == ItemKind.Consumable && effect is not null;
}

public sealed record SkillLearnDef
{
    public required string skillId { get; init; }
    public int level { get; init; } = 1;
}

public sealed record CharacterDef
{
    public required string id { get; init; }
    public required string name { get; init; }

    /// <summary>Stats at level 1, before equipment.</summary>
    public required StatBlock baseStats { get; init; }

    /// <summary>Added once per level above 1.</summary>
    public required StatBlock growth { get; init; }

    public IReadOnlyList<SkillLearnDef> skills { get; init; } = [];

    /// <summary>Item ids equipped when the character joins; each goes to its own slot.</summary>
    public IReadOnlyList<string> initialEquipment { get; init; } = [];

    /// <summary>Battle narration after the name for a plain attack, e.g. "揮劍斬擊！". Empty = a generic line.</summary>
    public string attackMessage { get; init; } = "";
}

public sealed record EnemyActionDef
{
    /// <summary>Skill id, or null for a plain attack.</summary>
    public string? skillId { get; init; }

    public int weight { get; init; } = 1;
}

public sealed record DropDef
{
    public required string itemId { get; init; }

    /// <summary>0–1.</summary>
    public required double chance { get; init; }
}

public sealed record EnemyDef
{
    public required string id { get; init; }
    public required string name { get; init; }
    public int level { get; init; } = 1;
    public required StatBlock stats { get; init; }
    public int exp { get; init; }
    public int gold { get; init; }

    /// <summary>Weighted action table the AI picks from. Empty = always attack.</summary>
    public IReadOnlyList<EnemyActionDef> actions { get; init; } = [];

    public IReadOnlyList<DropDef> drops { get; init; } = [];

    /// <summary>Elements that deal ×1.5 damage to it.</summary>
    public IReadOnlyList<Element> weakTo { get; init; } = [];

    /// <summary>Elements that deal ×0.5 damage to it.</summary>
    public IReadOnlyList<Element> resists { get; init; } = [];

    /// <summary>Statuses that never take hold (bosses are usually immune to Sleep and Paralysis).</summary>
    public IReadOnlyList<StatusKind> immuneTo { get; init; } = [];

    /// <summary>Enemy id that takes this one's place when it falls (a boss's second form). Rewards of both are paid.</summary>
    public string? nextPhase { get; init; }

    /// <summary>Narration when <see cref="nextPhase"/> appears.</summary>
    public string phaseMessage { get; init; } = "";

    /// <summary>Battle narration after the name for a plain attack, e.g. "撲了過來！". Empty = a generic line.</summary>
    public string attackMessage { get; init; } = "";

    /// <summary>ASCII art, one markup string per line.</summary>
    public IReadOnlyList<string> art { get; init; } = [];
}

public sealed record StartingMemberDef
{
    public required string characterId { get; init; }
    public int level { get; init; } = 1;
}

public sealed record ItemStackDef
{
    public required string itemId { get; init; }
    public int count { get; init; } = 1;
}

/// <summary>The state a new game starts from (content/newGame.json).</summary>
public sealed record NewGameDef
{
    public required IReadOnlyList<StartingMemberDef> party { get; init; }
    public IReadOnlyList<ItemStackDef> items { get; init; } = [];
    public int gold { get; init; }

    /// <summary>Where the party starts (and is carried back to after a defeat until they rest at an inn).</summary>
    public string? location { get; init; }
}
