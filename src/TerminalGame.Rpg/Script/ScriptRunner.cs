using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Script;

// ── Requests: things a script needs the UI to show or do before it can continue ─────────────────

public abstract record ScriptRequest;

public sealed record SayRequest(string text, string? speaker) : ScriptRequest;

/// <summary>Answer with <see cref="ScriptRunner.choose"/>; the index is into <see cref="labels"/> (hidden options already removed).</summary>
public sealed record ChoiceRequest(IReadOnlyList<string> labels) : ScriptRequest;

public sealed record ItemGainedRequest(ItemDef item, int count) : ScriptRequest;

public sealed record ItemLostRequest(ItemDef item, int count) : ScriptRequest;

/// <summary><paramref name="amount"/> is negative when gold was taken.</summary>
public sealed record GoldChangedRequest(int amount) : ScriptRequest;

public sealed record MemberJoinedRequest(PartyMember member) : ScriptRequest;

/// <summary>Answer with <see cref="ScriptRunner.battleFinished"/>. Anything but a victory ends the script.</summary>
public sealed record BattleRequest(IReadOnlyList<string> enemyIds, bool canEscape) : ScriptRequest;

public sealed record ShopRequest(ShopDef shop) : ScriptRequest;

/// <summary>The UI asks whether to stay, takes the gold, restores the party and offers to save.</summary>
public sealed record InnRequest(int price) : ScriptRequest;

/// <summary>The party has moved (<see cref="GameSession.locationId"/> is already updated); the UI should show the new place.</summary>
public sealed record TravelRequest(LocationDef location) : ScriptRequest;

/// <summary>
/// Interprets one event script against a <see cref="GameSession"/>. State changes (flags, items, gold, party) are
/// applied here; whenever the script needs the player to see or decide something it stops and exposes a
/// <see cref="ScriptRequest"/>. The UI handles it and calls <see cref="next"/>, <see cref="choose"/> or
/// <see cref="battleFinished"/>, which run on to the following request (or null when the script is done).
/// </summary>
public sealed class ScriptRunner
{
    /// <summary>Guards against scripts that <c>run</c> each other forever.</summary>
    public const int maxDepth = 32;

    private sealed class Frame
    {
        public Frame(IReadOnlyList<ScriptCommandDef> commands)
        {
            this.commands = commands;
        }

        public IReadOnlyList<ScriptCommandDef> commands { get; }
        public int index { get; set; }
    }

    private readonly GameSession session;
    private readonly Stack<Frame> frames = new();
    private IReadOnlyList<ChoiceDef> visibleChoices = [];

    public ScriptRunner(GameSession session, IReadOnlyList<ScriptCommandDef> commands)
    {
        this.session = session;
        frames.Push(new Frame(commands));
    }

    public static ScriptRunner forScript(GameSession session, string scriptId) =>
        new(session, session.content.script(scriptId));

    /// <summary>The request waiting for the UI; null before <see cref="start"/> and after the script ends.</summary>
    public ScriptRequest? current { get; private set; }

    public bool isFinished => frames.Count == 0 && current is null;

    public ScriptRequest? start() => run();

    /// <summary>Acknowledges any request other than a choice or a battle.</summary>
    public ScriptRequest? next()
    {
        if (current is null or ChoiceRequest or BattleRequest)
        {
            throw new InvalidOperationException($"cannot simply continue from {current?.GetType().Name ?? "nothing"}");
        }

        return run();
    }

    public ScriptRequest? choose(int index)
    {
        if (current is not ChoiceRequest)
        {
            throw new InvalidOperationException("no choice is pending");
        }

        ChoiceDef picked = visibleChoices[index];
        current = null;
        push(picked.then);
        return run();
    }

    public ScriptRequest? battleFinished(BattleOutcome outcome)
    {
        if (current is not BattleRequest)
        {
            throw new InvalidOperationException("no battle is pending");
        }

        if (outcome != BattleOutcome.Victory)
        {
            frames.Clear();
            current = null;
            return null;
        }

        return run();
    }

    private void push(IReadOnlyList<ScriptCommandDef> commands)
    {
        if (frames.Count >= maxDepth)
        {
            throw new InvalidOperationException("script nesting too deep (recursive 'run'?)");
        }

        frames.Push(new Frame(commands));
    }

    /// <summary>Executes commands until one needs the UI.</summary>
    private ScriptRequest? run()
    {
        current = null;
        while (frames.Count > 0)
        {
            Frame frame = frames.Peek();
            if (frame.index >= frame.commands.Count)
            {
                frames.Pop();
                continue;
            }

            ScriptCommandDef command = frame.commands[frame.index++];
            ScriptRequest? request = execute(command);
            if (request is not null)
            {
                current = request;
                return request;
            }
        }

        return null;
    }

    private ScriptRequest? execute(ScriptCommandDef c)
    {
        ContentDb content = session.content;
        if (c.say is not null)
        {
            return new SayRequest(c.say, c.speaker);
        }

        if (c.@if is not null)
        {
            IReadOnlyList<ScriptCommandDef>? branch = Condition.check(c.@if, session) ? c.then : c.@else;
            if (branch is not null)
            {
                push(branch);
            }

            return null;
        }

        if (c.choice is not null)
        {
            visibleChoices = c.choice.Where(o => Condition.check(o.@if, session)).ToList();
            return visibleChoices.Count == 0 ? null : new ChoiceRequest(visibleChoices.Select(o => o.label).ToList());
        }

        if (c.cases is not null)
        {
            CaseDef? match = c.cases.FirstOrDefault(o => Condition.check(o.@if, session));
            if (match is not null)
            {
                push(match.then);
            }

            return null;
        }

        if (c.oneOf is not null)
        {
            List<CaseDef> matches = c.oneOf.Where(o => Condition.check(o.@if, session)).ToList();
            if (matches.Count > 0)
            {
                push(matches[session.random.Next(matches.Count)].then);
            }

            return null;
        }

        if (c.setFlag is not null)
        {
            session.flags[c.setFlag] = c.value;
            return null;
        }

        if (c.addFlag is not null)
        {
            session.flags[c.addFlag] += c.value;
            return null;
        }

        if (c.giveItem is not null)
        {
            int added = session.inventory.add(c.giveItem, c.count);
            return new ItemGainedRequest(content.item(c.giveItem), added);
        }

        if (c.takeItem is not null)
        {
            int taken = Math.Min(c.count, session.inventory.count(c.takeItem));
            session.inventory.remove(c.takeItem, taken);
            return taken > 0 ? new ItemLostRequest(content.item(c.takeItem), taken) : null;
        }

        if (c.giveGold is int gain)
        {
            session.gold += gain;
            return new GoldChangedRequest(gain);
        }

        if (c.takeGold is int loss)
        {
            int taken = Math.Min(loss, session.gold);
            session.gold -= taken;
            return new GoldChangedRequest(-taken);
        }

        if (c.join is not null)
        {
            if (session.party.Any(m => m.def.id == c.join))
            {
                return null;
            }

            // Newcomers never lag far behind a party that has been levelling: at least the party's average level.
            int level = Math.Max(c.level, session.party.Count == 0 ? 1 : (int)session.party.Average(m => m.level));
            return new MemberJoinedRequest(session.addMember(c.join, level));
        }

        if (c.battle is not null)
        {
            return new BattleRequest(c.battle, c.canEscape);
        }

        if (c.shop is not null)
        {
            return new ShopRequest(content.shop(c.shop));
        }

        if (c.inn is int price)
        {
            return new InnRequest(price);
        }

        if (c.travel is not null)
        {
            session.locationId = c.travel;
            return new TravelRequest(content.location(c.travel));
        }

        if (c.run is not null)
        {
            push(content.script(c.run));
            return null;
        }

        if (c.restore)
        {
            session.restoreParty();
            return null;
        }

        if (c.end)
        {
            frames.Clear();
            return null;
        }

        throw new InvalidOperationException("empty script command");
    }
}
