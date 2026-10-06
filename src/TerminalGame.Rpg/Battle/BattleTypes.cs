using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Battle;

public enum Effectiveness
{
    Normal,

    /// <summary>The target is weak to the element.</summary>
    Weak,

    Resisted,
}

public enum StatusEndReason
{
    /// <summary>Its turns ran out (for Sleep: woke up naturally).</summary>
    Expired,

    Cured,

    /// <summary>Sleep broken by taking damage.</summary>
    WokeUp,

    /// <summary>A buff and its debuff met and cancelled out.</summary>
    Cancelled,
}

public enum BattleOutcome
{
    Ongoing,
    Victory,
    Defeat,
    Escaped,
}

// ── Actions: what the current actor decided to do ─────────────────────────────────────────────

public abstract record BattleAction(Combatant actor);

public sealed record AttackAction(Combatant actor, Combatant target) : BattleAction(actor);

/// <summary><paramref name="target"/> is ignored for Self/All* skills; a dead or missing single target is re-picked.</summary>
public sealed record SkillAction(Combatant actor, SkillDef skill, Combatant? target) : BattleAction(actor);

public sealed record ItemAction(Combatant actor, ItemDef item, Combatant? target) : BattleAction(actor);

public sealed record GuardAction(Combatant actor) : BattleAction(actor);

public sealed record EscapeAction(Combatant actor) : BattleAction(actor);

// ── Events: what happened, in order. The UI plays these back; the engine never touches the screen. ──
// HP/MP events carry the value *after* the change so the UI can animate bars in step with the messages.

public abstract record BattleEvent;

public sealed record RoundStartedEvent(int round) : BattleEvent;

public sealed record TurnStartedEvent(Combatant actor) : BattleEvent;

public sealed record AttackEvent(Combatant actor) : BattleEvent;

public sealed record SkillUsedEvent(Combatant actor, SkillDef skill, int remainingMp) : BattleEvent;

public sealed record ItemUsedEvent(Combatant actor, ItemDef item, int remainingCount) : BattleEvent;

public sealed record GuardEvent(Combatant actor) : BattleEvent;

public sealed record DamageEvent(Combatant target, int amount, bool isCritical, int remainingHp, Effectiveness effectiveness = Effectiveness.Normal)
    : BattleEvent;

public sealed record StatusAppliedEvent(Combatant target, StatusKind kind, int turns) : BattleEvent;

/// <summary>Only reported for pure status effects (a failed side effect of an attack is silent).</summary>
public sealed record StatusMissedEvent(Combatant target, StatusKind kind) : BattleEvent;

public sealed record StatusRemovedEvent(Combatant target, StatusKind kind, StatusEndReason reason) : BattleEvent;

/// <summary>The actor's turn passes without an action (asleep or paralysed).</summary>
public sealed record TurnSkippedEvent(Combatant actor, StatusKind cause) : BattleEvent;

public sealed record ReviveEvent(Combatant target, int remainingHp) : BattleEvent;

/// <summary><paramref name="next"/> took <paramref name="previous"/>'s place (same slot); it acts from the next round.</summary>
public sealed record PhaseChangedEvent(EnemyCombatant previous, EnemyCombatant next) : BattleEvent;

public sealed record PoisonDamageEvent(Combatant target, int amount, int remainingHp) : BattleEvent;

public sealed record HealEvent(Combatant target, int amount, int remainingHp) : BattleEvent;

public sealed record MpRestoreEvent(Combatant target, int amount, int remainingMp) : BattleEvent;

public sealed record DefeatedEvent(Combatant target) : BattleEvent;

public sealed record EscapeEvent(Combatant actor, bool succeeded) : BattleEvent;

public sealed record VictoryEvent(int exp, int gold, IReadOnlyList<ItemDef> drops) : BattleEvent;

public sealed record LevelUpEvent(LevelUp levelUp) : BattleEvent;

public sealed record PartyDefeatedEvent : BattleEvent;
