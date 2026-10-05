using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Battle;

public enum Side
{
    Party,
    Enemies,
}

/// <summary>Anyone taking part in a battle. Party combatants write HP/MP straight through to their <see cref="PartyMember"/>.</summary>
public abstract class Combatant
{
    protected Combatant(Side side, int slot)
    {
        this.side = side;
        this.slot = slot;
    }

    public Side side { get; }

    /// <summary>Position within its side (0-based), stable for the whole battle.</summary>
    public int slot { get; }

    public abstract string name { get; }
    public abstract StatBlock stats { get; }
    public abstract int hp { get; set; }
    public abstract int mp { get; set; }
    public bool isAlive => hp > 0;

    /// <summary>Halves incoming damage until this combatant's next turn starts.</summary>
    public bool isGuarding { get; internal set; }

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
    public override StatBlock stats => member.stats;

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
    public override StatBlock stats => def.stats;

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
