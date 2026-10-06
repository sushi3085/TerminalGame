using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Battle;

public enum Side
{
    Party,
    Enemies,
}

/// <summary>A status currently affecting a combatant.</summary>
public sealed class ActiveStatus
{
    internal ActiveStatus(StatusKind kind, int turnsLeft)
    {
        this.kind = kind;
        this.turnsLeft = turnsLeft;
    }

    public StatusKind kind { get; }

    /// <summary>Own turns left, including the current one.</summary>
    public int turnsLeft { get; internal set; }

    /// <summary>Set when applied during the holder's own turn, so that turn does not count toward the duration.</summary>
    internal bool skipNextTick { get; set; }
}

/// <summary>
/// Anyone taking part in a battle. Party combatants write HP/MP straight through to their <see cref="PartyMember"/>;
/// statuses live here only, so they are gone once the battle ends.
/// </summary>
public abstract class Combatant
{
    private readonly List<ActiveStatus> active = new();

    protected Combatant(Side side, int slot)
    {
        this.side = side;
        this.slot = slot;
    }

    public Side side { get; }

    /// <summary>Position within its side (0-based), stable for the whole battle.</summary>
    public int slot { get; }

    public abstract string name { get; }

    /// <summary>Stats before statuses.</summary>
    public abstract StatBlock baseStats { get; }

    /// <summary>Stats with attack/defense buffs and debuffs applied; max HP/MP are never changed.</summary>
    public StatBlock stats => StatusRules.modify(baseStats, active);
    public abstract int hp { get; set; }
    public abstract int mp { get; set; }
    public bool isAlive => hp > 0;

    /// <summary>Halves incoming damage until this combatant's next turn starts.</summary>
    public bool isGuarding { get; internal set; }

    public IReadOnlyList<ActiveStatus> statuses => active;

    public bool has(StatusKind kind) => active.Any(s => s.kind == kind);

    /// <summary>Damage multiplier for an element: 1.5 weak, 0.5 resisted, 1 otherwise.</summary>
    public virtual double elementRate(Element element) => 1;

    public virtual bool isImmuneTo(StatusKind kind) => false;

    internal ActiveStatus? find(StatusKind kind) => active.FirstOrDefault(s => s.kind == kind);

    internal void add(ActiveStatus status) => active.Add(status);

    internal void remove(ActiveStatus status) => active.Remove(status);

    internal void clearStatuses() => active.Clear();

    public override string ToString() => $"{name} ({side} #{slot}, HP {hp})";
}

public sealed class PartyCombatant : Combatant
{
    public PartyCombatant(PartyMember member, int slot)
        : base(Side.Party, slot)
    {
        this.member = member;
    }

    public PartyMember member { get; }
    public override string name => member.name;
    public override StatBlock baseStats => member.stats;

    public override int hp
    {
        get => member.hp;
        set => member.hp = value;
    }

    public override int mp
    {
        get => member.mp;
        set => member.mp = value;
    }
}

public sealed class EnemyCombatant : Combatant
{
    private int currentHp;
    private int currentMp;

    public EnemyCombatant(EnemyDef def, int slot, string name)
        : base(Side.Enemies, slot)
    {
        this.def = def;
        this.name = name;
        currentHp = def.stats.maxHp;
        currentMp = def.stats.maxMp;
    }

    public EnemyDef def { get; }
    public override string name { get; }
    public override StatBlock baseStats => def.stats;

    public override double elementRate(Element element) =>
        element == Element.None ? 1
        : def.weakTo.Contains(element) ? StatusRules.weakMultiplier
        : def.resists.Contains(element) ? StatusRules.resistMultiplier
        : 1;

    public override bool isImmuneTo(StatusKind kind) => def.immuneTo.Contains(kind);

    public override int hp
    {
        get => currentHp;
        set => currentHp = Math.Clamp(value, 0, def.stats.maxHp);
    }

    public override int mp
    {
        get => currentMp;
        set => currentMp = Math.Clamp(value, 0, def.stats.maxMp);
    }
}
