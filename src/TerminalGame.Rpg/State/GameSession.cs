using TerminalGame.Rpg.Data;

namespace TerminalGame.Rpg.State;

/// <summary>Story progress as named integers ("forestCleared" = 1, "talkedToElder" = 3…). Unset flags read as 0.</summary>
public sealed class Flags
{
    private readonly Dictionary<string, int> values = new();

    public int this[string name]
    {
        get => values.GetValueOrDefault(name);
        set
        {
            if (value == 0)
            {
                values.Remove(name);
            }
            else
            {
                values[name] = value;
            }
        }
    }

    public bool isSet(string name) => values.ContainsKey(name);

    public IReadOnlyDictionary<string, int> all => values;
}

/// <summary>Everything that changes during one playthrough. This is what a save file will hold.</summary>
public sealed class GameSession
{
    public const int maxPartySize = 4;

    private readonly List<PartyMember> members = new();

    public GameSession(ContentDb content, int? seed = null)
    {
        this.content = content;
        random = seed is null ? new Random() : new Random(seed.Value);
    }

    public ContentDb content { get; }
    public Random random { get; }
    public IReadOnlyList<PartyMember> party => members;
    public Inventory inventory { get; } = new();
    public Flags flags { get; } = new();
    public int gold { get; set; }

    public bool isPartyDefeated => members.All(m => !m.isAlive);

    /// <summary>Starts a game from <see cref="ContentDb.newGame"/>.</summary>
    public static GameSession newGame(ContentDb content, int? seed = null)
    {
        NewGameDef start = content.newGame ?? throw new InvalidOperationException($"content has no {ContentDb.newGameFile}");
        GameSession session = new(content, seed) { gold = start.gold };
        foreach (StartingMemberDef member in start.party)
        {
            session.addMember(member.characterId, member.level);
        }

        foreach (ItemStackDef stack in start.items)
        {
            session.inventory.add(stack.itemId, stack.count);
        }

        return session;
    }

    public PartyMember addMember(string characterId, int level = 1)
    {
        if (members.Count >= maxPartySize)
        {
            throw new InvalidOperationException("party is full");
        }

        if (members.Any(m => m.def.id == characterId))
        {
            throw new InvalidOperationException($"'{characterId}' is already in the party");
        }

        PartyMember member = new(content, content.character(characterId), level);
        members.Add(member);
        return member;
    }

    public void restoreParty()
    {
        foreach (PartyMember member in members)
        {
            member.restore();
        }
    }
}
