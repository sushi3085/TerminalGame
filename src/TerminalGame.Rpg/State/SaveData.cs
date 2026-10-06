using System.Text.Json;
using System.Text.Json.Serialization;
using TerminalGame.Rpg.Data;

namespace TerminalGame.Rpg.State;

public sealed record SavedMember
{
    public required string characterId { get; init; }
    public int level { get; init; } = 1;
    public int exp { get; init; }
    public int hp { get; init; }
    public int mp { get; init; }
    public Dictionary<EquipSlot, string> equipment { get; init; } = new();
}

/// <summary>
/// A save file: only ids and numbers, never definitions, so rebalancing content does not break old saves.
/// Items or equipment whose ids no longer exist are dropped on load; a missing character is an error.
/// </summary>
public sealed record SaveData
{
    public const int currentVersion = 1;

    public int version { get; init; } = currentVersion;
    public DateTime savedAt { get; init; }
    public double playSeconds { get; init; }
    public string? locationId { get; init; }
    public string? respawnLocationId { get; init; }
    public int gold { get; init; }
    public List<SavedMember> party { get; init; } = new();
    public List<ItemStackDef> inventory { get; init; } = new();
    public Dictionary<string, int> flags { get; init; } = new();

    /// <summary>Lead member's name and level, for the title screen.</summary>
    public string summary(ContentDb content)
    {
        SavedMember? lead = party.FirstOrDefault();
        string who = lead is not null && content.hasCharacter(lead.characterId)
            ? $"{content.character(lead.characterId).name} Lv.{lead.level}"
            : "?";
        string where = locationId is not null && content.hasLocation(locationId)
            ? content.location(locationId).name
            : "";
        TimeSpan time = TimeSpan.FromSeconds(playSeconds);
        return $"{who}  {where}  {(int)time.TotalHours:00}:{time.Minutes:00}";
    }
}

public sealed class SaveException : Exception
{
    public SaveException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

public static class SaveSystem
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static SaveData capture(GameSession session) => new()
    {
        savedAt = DateTime.UtcNow,
        playSeconds = session.playSeconds,
        locationId = session.locationId,
        respawnLocationId = session.respawnLocationId,
        gold = session.gold,
        party = session.party.Select(m => new SavedMember
        {
            characterId = m.def.id,
            level = m.level,
            exp = m.exp,
            hp = m.hp,
            mp = m.mp,
            equipment = m.equipment.ToDictionary(e => e.Key, e => e.Value),
        }).ToList(),
        inventory = session.inventory.entries.Select(e => new ItemStackDef { itemId = e.itemId, count = e.count }).ToList(),
        flags = session.flags.all.ToDictionary(f => f.Key, f => f.Value),
    };

    public static GameSession restore(ContentDb content, SaveData data, int? seed = null)
    {
        if (data.version > SaveData.currentVersion)
        {
            throw new SaveException($"save version {data.version} is newer than this game ({SaveData.currentVersion})");
        }

        bool knownLocation(string? id) => id is not null && content.hasLocation(id);
        GameSession session = new(content, seed)
        {
            gold = Math.Max(0, data.gold),
            playSeconds = data.playSeconds,
            locationId = knownLocation(data.locationId) ? data.locationId : content.newGame?.location,
            respawnLocationId = knownLocation(data.respawnLocationId) ? data.respawnLocationId : content.newGame?.location,
        };

        foreach (SavedMember saved in data.party)
        {
            if (!content.hasCharacter(saved.characterId))
            {
                throw new SaveException($"save refers to unknown character '{saved.characterId}'");
            }

            session.addMember(saved.characterId, saved.level).load(saved.level, saved.exp, saved.equipment, saved.hp, saved.mp);
        }

        if (session.party.Count == 0)
        {
            throw new SaveException("save has an empty party");
        }

        foreach (ItemStackDef stack in data.inventory.Where(s => content.hasItem(s.itemId)))
        {
            session.inventory.add(stack.itemId, stack.count);
        }

        foreach ((string name, int value) in data.flags)
        {
            session.flags[name] = value;
        }

        return session;
    }

    public static string serialize(SaveData data) => JsonSerializer.Serialize(data, jsonOptions);

    public static SaveData deserialize(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<SaveData>(json, jsonOptions) ?? throw new SaveException("empty save file");
        }
        catch (JsonException ex)
        {
            throw new SaveException("corrupt save file", ex);
        }
    }
}

/// <summary>Numbered save slots as JSON files in one directory.</summary>
public sealed class SaveStore
{
    public SaveStore(string directory)
    {
        this.directory = directory;
    }

    public string directory { get; }

    /// <summary>~/.local/share/TerminalGame/saves (or the platform's equivalent).</summary>
    public static SaveStore userDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.Create),
        "TerminalGame",
        "saves"));

    public string pathFor(int slot) => Path.Combine(directory, $"slot{slot}.json");

    public bool exists(int slot) => File.Exists(pathFor(slot));

    public void save(GameSession session, int slot)
    {
        Directory.CreateDirectory(directory);
        // Write to a temp file first so a crash mid-write never destroys the previous save.
        string path = pathFor(slot);
        string temp = path + ".tmp";
        File.WriteAllText(temp, SaveSystem.serialize(SaveSystem.capture(session)));
        File.Move(temp, path, overwrite: true);
    }

    /// <summary>The slot's data, or null if empty or unreadable.</summary>
    public SaveData? peek(int slot)
    {
        if (!exists(slot))
        {
            return null;
        }

        try
        {
            return SaveSystem.deserialize(File.ReadAllText(pathFor(slot)));
        }
        catch (SaveException)
        {
            return null;
        }
    }

    public GameSession load(ContentDb content, int slot)
    {
        SaveData data = peek(slot) ?? throw new SaveException($"slot {slot} is empty or unreadable");
        return SaveSystem.restore(content, data);
    }
}
