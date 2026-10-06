using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Battle;

/// <summary>
/// Turn-based battle rules, independent of any UI.
/// <para>
/// Each round, every living combatant acts once, fastest first (speed with ±10% jitter). The caller drives it:
/// call <see cref="start"/>, then repeatedly look at <see cref="currentActor"/> and pass its action to
/// <see cref="execute"/> — a player's choice for party members, <see cref="decideEnemyAction"/> for enemies —
/// until <see cref="outcome"/> is no longer <see cref="BattleOutcome.Ongoing"/>. Every call returns the events
/// it produced so a UI can narrate them; tests and the balance simulator just ignore them.
/// </para>
/// <para>
/// Statuses tick at the end of their holder's turn (poison damage, then one turn off every duration). A sleeping or
/// paralysed combatant's turn is played out inside the engine, so <see cref="currentActor"/> is always someone who
/// can act.
/// </para>
/// </summary>
public sealed class BattleEngine
{
    public const double turnOrderJitter = 0.1;

    private static readonly EffectDef basicAttack = new() { kind = EffectKind.Physical, power = 100 };

    private readonly GameSession session;
    private readonly List<Combatant> partySide = new();
    private readonly List<Combatant> enemySide = new();
    private readonly Queue<Combatant> turnQueue = new();

    public BattleEngine(GameSession session, IEnumerable<EnemyDef> enemies, bool canEscape = true)
    {
        this.session = session;
        this.canEscape = canEscape;

        for (int i = 0; i < session.party.Count; i++)
        {
            partySide.Add(new PartyCombatant(session.party[i], i));
        }

        List<EnemyDef> enemyList = enemies.ToList();
        if (enemyList.Count == 0)
        {
            throw new ArgumentException("a battle needs at least one enemy", nameof(enemies));
        }

        for (int i = 0; i < enemyList.Count; i++)
        {
            EnemyDef def = enemyList[i];
            // Two or more of the same kind get letter suffixes: 史萊姆 A, 史萊姆 B.
            int sameKind = enemyList.Count(e => e.id == def.id);
            string name = sameKind > 1 ? $"{def.name} {(char)('A' + enemyList.Take(i).Count(e => e.id == def.id))}" : def.name;
            enemySide.Add(new EnemyCombatant(def, i, name));
        }
    }

    public IReadOnlyList<Combatant> party => partySide;
    public IReadOnlyList<Combatant> enemies => enemySide;
    public bool canEscape { get; }
    public int round { get; private set; }
    public Combatant? currentActor { get; private set; }
    public BattleOutcome outcome { get; private set; } = BattleOutcome.Ongoing;
    public Random random => session.random;

    public IReadOnlyList<Combatant> alliesOf(Combatant c) => (c.side == Side.Party ? partySide : enemySide).Where(x => x.isAlive).ToList();

    public IReadOnlyList<Combatant> opponentsOf(Combatant c) => (c.side == Side.Party ? enemySide : partySide).Where(x => x.isAlive).ToList();

    public IReadOnlyList<SkillDef> skillsOf(Combatant c) => c switch
    {
        PartyCombatant p => p.member.skills,
        EnemyCombatant e => e.def.actions.Where(a => a.skillId is not null).Select(a => session.content.skill(a.skillId!)).Distinct().ToList(),
        _ => [],
    };

    public bool canAfford(Combatant c, SkillDef skill) => c.mp >= skill.mpCost;

    /// <summary>Begins round 1 and picks the first actor.</summary>
    public IReadOnlyList<BattleEvent> start()
    {
        if (round != 0)
        {
            throw new InvalidOperationException("battle already started");
        }

        List<BattleEvent> events = new();
        if (partySide.All(c => !c.isAlive))
        {
            outcome = BattleOutcome.Defeat;
            events.Add(new PartyDefeatedEvent());
            return events;
        }

        advance(events);
        return events;
    }

    public IReadOnlyList<BattleEvent> execute(BattleAction action)
    {
        if (outcome != BattleOutcome.Ongoing)
        {
            throw new InvalidOperationException($"battle is over ({outcome})");
        }

        if (!ReferenceEquals(action.actor, currentActor))
        {
            throw new InvalidOperationException($"it is not {action.actor.name}'s turn");
        }

        List<BattleEvent> events = new();
        switch (action)
        {
            case AttackAction attack:
                events.Add(new AttackEvent(attack.actor));
                applyEffect(attack.actor, resolveTargets(attack.actor, TargetKind.SingleEnemy, attack.target), basicAttack, events);
                break;
            case SkillAction skill:
                useSkill(skill, events);
                break;
            case ItemAction item:
                useItem(item, events);
                break;
            case GuardAction guard:
                guard.actor.isGuarding = true;
                events.Add(new GuardEvent(guard.actor));
                break;
            case EscapeAction escape:
                tryEscape(escape, events);
                break;
            default:
                throw new ArgumentException($"unknown action {action.GetType().Name}", nameof(action));
        }

        if (outcome == BattleOutcome.Ongoing && !checkForEnd(events))
        {
            endTurn(action.actor, events);
            if (!checkForEnd(events))
            {
                advance(events);
            }
        }

        return events;
    }

    /// <summary>Simple weighted-random AI for the current enemy: picks from its action table, skipping skills it cannot afford.</summary>
    public BattleAction decideEnemyAction()
    {
        if (currentActor is not EnemyCombatant enemy)
        {
            throw new InvalidOperationException("the current actor is not an enemy");
        }

        List<(SkillDef? skill, int weight)> options = new();
        foreach (EnemyActionDef entry in enemy.def.actions)
        {
            SkillDef? skill = entry.skillId is null ? null : session.content.skill(entry.skillId);
            // A buff the enemy already has would be a wasted turn.
            bool pointless = skill is { target: TargetKind.Self, effect.status: { } buff } && enemy.has(buff.kind);
            if (skill is null || (canAfford(enemy, skill) && !pointless))
            {
                options.Add((skill, entry.weight));
            }
        }

        SkillDef? chosen = null;
        if (options.Count > 0)
        {
            int roll = random.Next(options.Sum(o => o.weight));
            foreach ((SkillDef? skill, int weight) in options)
            {
                if (roll < weight)
                {
                    chosen = skill;
                    break;
                }

                roll -= weight;
            }
        }

        if (chosen is null)
        {
            return new AttackAction(enemy, pickRandom(opponentsOf(enemy)));
        }

        Combatant? target = chosen.target switch
        {
            TargetKind.SingleEnemy => pickRandom(opponentsOf(enemy)),
            TargetKind.SingleAlly => mostHurt(alliesOf(enemy)),
            _ => null,
        };
        return new SkillAction(enemy, chosen, target);
    }

    private void useSkill(SkillAction action, List<BattleEvent> events)
    {
        Combatant actor = action.actor;
        if (!skillsOf(actor).Contains(action.skill))
        {
            throw new InvalidOperationException($"{actor.name} does not know {action.skill.id}");
        }

        if (!canAfford(actor, action.skill))
        {
            throw new InvalidOperationException($"{actor.name} lacks MP for {action.skill.id}");
        }

        actor.mp -= action.skill.mpCost;
        events.Add(new SkillUsedEvent(actor, action.skill, actor.mp));
        applyEffect(actor, resolveTargets(actor, action.skill.target, action.target), action.skill.effect, events);
    }

    private void useItem(ItemAction action, List<BattleEvent> events)
    {
        Combatant actor = action.actor;
        ItemDef item = action.item;
        if (actor.side != Side.Party)
        {
            throw new InvalidOperationException("only the party uses items");
        }

        if (!item.usableInBattle || !session.inventory.remove(item.id))
        {
            throw new InvalidOperationException($"cannot use {item.id}");
        }

        events.Add(new ItemUsedEvent(actor, item, session.inventory.count(item.id)));
        applyEffect(actor, resolveTargets(actor, item.target, action.target), item.effect!, events);
    }

    private void tryEscape(EscapeAction action, List<BattleEvent> events)
    {
        if (!canEscape || action.actor.side != Side.Party)
        {
            throw new InvalidOperationException("cannot escape from this battle");
        }

        double partySpeed = alliesOf(action.actor).Average(c => c.stats.speed);
        double enemySpeed = opponentsOf(action.actor).Average(c => c.stats.speed);
        bool succeeded = random.NextDouble() < DamageFormula.escapeChance(partySpeed, enemySpeed);
        events.Add(new EscapeEvent(action.actor, succeeded));
        if (succeeded)
        {
            outcome = BattleOutcome.Escaped;
            currentActor = null;
        }
    }

    private IReadOnlyList<Combatant> resolveTargets(Combatant actor, TargetKind kind, Combatant? chosen)
    {
        IReadOnlyList<Combatant> allies = alliesOf(actor);
        IReadOnlyList<Combatant> opponents = opponentsOf(actor);
        return kind switch
        {
            TargetKind.Self => [actor],
            TargetKind.AllAllies => allies,
            TargetKind.AllEnemies => opponents,
            TargetKind.SingleAlly => [chosen is not null && allies.Contains(chosen) ? chosen : mostHurt(allies)],
            TargetKind.SingleEnemy => [chosen is not null && opponents.Contains(chosen) ? chosen : pickRandom(opponents)],
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
    }

    private void applyEffect(Combatant user, IReadOnlyList<Combatant> targets, EffectDef effect, List<BattleEvent> events)
    {
        foreach (Combatant target in targets)
        {
            switch (effect.kind)
            {
                case EffectKind.Physical:
                case EffectKind.Magical:
                    double rate = target.elementRate(effect.element);
                    (int amount, bool isCritical) = DamageFormula.rollDamage(user.stats, target.stats, effect, target.isGuarding, random, rate);
                    target.hp -= amount;
                    events.Add(new DamageEvent(target, amount, isCritical, target.hp, StatusRules.effectivenessOf(rate)));
                    if (!target.isAlive)
                    {
                        defeat(target, events);
                        continue;
                    }

                    if (target.find(StatusKind.Sleep) is { } sleep)
                    {
                        removeStatus(target, sleep, StatusEndReason.WokeUp, events);
                    }

                    break;
                case EffectKind.Heal:
                    int before = target.hp;
                    target.hp += DamageFormula.rollHeal(user.stats, effect, random);
                    events.Add(new HealEvent(target, target.hp - before, target.hp));
                    break;
                case EffectKind.RestoreMp:
                    int mpBefore = target.mp;
                    target.mp += effect.power;
                    events.Add(new MpRestoreEvent(target, target.mp - mpBefore, target.mp));
                    break;
            }

            foreach (StatusKind kind in effect.cures)
            {
                if (target.find(kind) is { } cured)
                {
                    removeStatus(target, cured, StatusEndReason.Cured, events);
                }
            }

            if (effect.status is not null)
            {
                inflict(target, effect.status, reportMiss: effect.kind == EffectKind.Status, events);
            }
        }
    }

    private void inflict(Combatant target, StatusEffectDef status, bool reportMiss, List<BattleEvent> events)
    {
        if (target.isImmuneTo(status.kind) || random.NextDouble() >= status.chance)
        {
            if (reportMiss)
            {
                events.Add(new StatusMissedEvent(target, status.kind));
            }

            return;
        }

        if (StatusRules.opposite(status.kind) is StatusKind opposite && target.find(opposite) is { } cancelled)
        {
            removeStatus(target, cancelled, StatusEndReason.Cancelled, events);
            return;
        }

        int turns = Math.Max(1, status.turns);
        ActiveStatus? existing = target.find(status.kind);
        if (existing is null)
        {
            existing = new ActiveStatus(status.kind, turns);
            target.add(existing);
        }
        else
        {
            existing.turnsLeft = Math.Max(existing.turnsLeft, turns);
        }

        existing.skipNextTick = ReferenceEquals(target, currentActor);
        events.Add(new StatusAppliedEvent(target, status.kind, turns));
    }

    private static void removeStatus(Combatant target, ActiveStatus status, StatusEndReason reason, List<BattleEvent> events)
    {
        target.remove(status);
        events.Add(new StatusRemovedEvent(target, status.kind, reason));
    }

    private static void defeat(Combatant target, List<BattleEvent> events)
    {
        target.isGuarding = false;
        target.clearStatuses();
        events.Add(new DefeatedEvent(target));
    }

    /// <summary>Poison damage, then every status of <paramref name="actor"/> loses a turn.</summary>
    private void endTurn(Combatant actor, List<BattleEvent> events)
    {
        if (!actor.isAlive)
        {
            return;
        }

        if (actor.has(StatusKind.Poison))
        {
            int amount = StatusRules.poisonDamage(actor.stats);
            actor.hp -= amount;
            events.Add(new PoisonDamageEvent(actor, amount, actor.hp));
            if (!actor.isAlive)
            {
                defeat(actor, events);
                return;
            }
        }

        foreach (ActiveStatus status in actor.statuses.ToList())
        {
            if (status.skipNextTick)
            {
                status.skipNextTick = false;
                continue;
            }

            if (--status.turnsLeft <= 0)
            {
                removeStatus(actor, status, StatusEndReason.Expired, events);
            }
        }
    }

    /// <summary>Whether <paramref name="actor"/> loses this turn to sleep or paralysis (reported as an event).</summary>
    private bool losesTurn(Combatant actor, List<BattleEvent> events)
    {
        StatusKind? cause = actor.has(StatusKind.Sleep) ? StatusKind.Sleep
            : actor.has(StatusKind.Paralysis) && random.NextDouble() < StatusRules.paralysisChance ? StatusKind.Paralysis
            : null;
        if (cause is StatusKind kind)
        {
            events.Add(new TurnSkippedEvent(actor, kind));
            return true;
        }

        return false;
    }

    /// <summary>Settles victory or defeat; returns true if the battle ended.</summary>
    private bool checkForEnd(List<BattleEvent> events)
    {
        if (enemySide.All(c => !c.isAlive))
        {
            outcome = BattleOutcome.Victory;
            currentActor = null;
            grantRewards(events);
            return true;
        }

        if (partySide.All(c => !c.isAlive))
        {
            outcome = BattleOutcome.Defeat;
            currentActor = null;
            events.Add(new PartyDefeatedEvent());
            return true;
        }

        return false;
    }

    private void grantRewards(List<BattleEvent> events)
    {
        int exp = 0;
        int gold = 0;
        List<ItemDef> drops = new();
        foreach (EnemyCombatant enemy in enemySide.Cast<EnemyCombatant>())
        {
            exp += enemy.def.exp;
            gold += enemy.def.gold;
            foreach (DropDef drop in enemy.def.drops)
            {
                if (random.NextDouble() < drop.chance)
                {
                    drops.Add(session.content.item(drop.itemId));
                }
            }
        }

        session.gold += gold;
        foreach (ItemDef item in drops)
        {
            session.inventory.add(item.id);
        }

        events.Add(new VictoryEvent(exp, gold, drops));

        // Everyone still standing gets the full amount; fallen members get nothing.
        foreach (PartyCombatant member in partySide.Cast<PartyCombatant>().Where(c => c.isAlive))
        {
            foreach (LevelUp levelUp in member.member.gainExp(exp))
            {
                events.Add(new LevelUpEvent(levelUp));
            }
        }
    }

    /// <summary>Moves on to the next combatant who can act, playing out turns lost to sleep or paralysis on the way.</summary>
    private void advance(List<BattleEvent> events)
    {
        while (true)
        {
            Combatant next = nextInOrder(events);
            currentActor = next;
            next.isGuarding = false;
            events.Add(new TurnStartedEvent(next));
            if (!losesTurn(next, events))
            {
                return;
            }

            endTurn(next, events);
            if (checkForEnd(events))
            {
                return;
            }
        }
    }

    /// <summary>The next living combatant in the turn queue, starting a new round when it runs out.</summary>
    private Combatant nextInOrder(List<BattleEvent> events)
    {
        while (true)
        {
            while (turnQueue.Count > 0)
            {
                Combatant next = turnQueue.Dequeue();
                if (next.isAlive)
                {
                    return next;
                }
            }

            round++;
            events.Add(new RoundStartedEvent(round));
            List<(Combatant combatant, double initiative)> order = partySide.Concat(enemySide)
                .Where(c => c.isAlive)
                .Select(c => (c, c.stats.speed * (1 + (random.NextDouble() * 2 - 1) * turnOrderJitter)))
                .ToList();
            // Ties go to the party, then to the lower slot, so the order is fully determined by the seed.
            foreach ((Combatant combatant, _) in order
                         .OrderByDescending(o => o.initiative)
                         .ThenBy(o => o.combatant.side)
                         .ThenBy(o => o.combatant.slot))
            {
                turnQueue.Enqueue(combatant);
            }
        }
    }

    private Combatant pickRandom(IReadOnlyList<Combatant> candidates) => candidates[random.Next(candidates.Count)];

    private static Combatant mostHurt(IReadOnlyList<Combatant> candidates) =>
        candidates.OrderBy(c => (double)c.hp / Math.Max(1, c.stats.maxHp)).ThenBy(c => c.slot).First();
}
