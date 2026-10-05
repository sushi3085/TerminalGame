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

    /// <summary>Where the party is; null only in sessions built without a world (tests, battle sandboxes).</summary>
    public string? locationId { get; set; }

    /// <summary>Where the party wakes up after losing a battle: the start, then the last inn they stayed at.</summary>
    public string? respawnLocationId { get; set; }

    public double playSeconds { get; set; }

    public bool isPartyDefeated => members.All(m => !m.isAlive);

    /// <summary>Starts a game from <see cref="ContentDb.newGame"/>.</summary>
    public static GameSession newGame(ContentDb content, int? seed = null)
    {
        NewGameDef start = content.newGame ?? throw new InvalidOperationException($"content has no {ContentDb.newGameFile}");
        GameSession session = new(content, seed)
        {
            gold = start.gold,
            locationId = start.location,
            respawnLocationId = start.location,
        };
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

    /// <summary>
    /// Equips an item from the inventory; whatever was in that slot goes back to the inventory.
    /// Returns false (and changes nothing) if the item is not in the inventory.
    /// </summary>
    public bool equipFromInventory(PartyMember member, ItemDef item)
    {
        if (item.kind != ItemKind.Equipment || !inventory.has(item.id))
        {
            return false;
        }

        inventory.remove(item.id);
        ItemDef? previous = member.equip(item);
        if (previous is not null)
        {
            inventory.add(previous.id);
        }

        return true;
    }

    public void unequipToInventory(PartyMember member, EquipSlot slot)
    {
        ItemDef? previous = member.unequip(slot);
        if (previous is not null)
        {
            inventory.add(previous.id);
        }
    }

    /// <summary>After a lost battle: lose half the gold, wake up fully healed at the respawn point.</summary>
    public int applyDefeat()
    {
        int lost = gold / 2;
        gold -= lost;
        restoreParty();
        locationId = respawnLocationId ?? locationId;
        return lost;
    }
}
