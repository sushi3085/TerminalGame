using System.Text.Json;
using System.Text.Json.Serialization;

namespace TerminalGame.Rpg.Data;

public sealed class ContentException : Exception
{
    public ContentException(IReadOnlyList<string> errors)
        : base("Invalid game content:\n  " + string.Join("\n  ", errors))
    {
        this.errors = errors;
    }

    public IReadOnlyList<string> errors { get; }
}

/// <summary>
/// All game definitions, indexed by id. Loaded once at startup from a directory of JSON files
/// (<c>skills.json</c>, <c>items.json</c>, <c>characters.json</c>, <c>enemies.json</c>, <c>newGame.json</c>);
/// every file is optional. Loading validates cross references, so a typo in an id fails at startup
/// instead of mid-game.
/// </summary>
public sealed class ContentDb
{
    public const string skillsFile = "skills.json";
    public const string itemsFile = "items.json";
    public const string charactersFile = "characters.json";
    public const string enemiesFile = "enemies.json";
    public const string newGameFile = "newGame.json";

    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly Dictionary<string, SkillDef> skillMap;
    private readonly Dictionary<string, ItemDef> itemMap;
    private readonly Dictionary<string, CharacterDef> characterMap;
    private readonly Dictionary<string, EnemyDef> enemyMap;

    private ContentDb(
        IReadOnlyList<SkillDef> skills,
        IReadOnlyList<ItemDef> items,
        IReadOnlyList<CharacterDef> characters,
        IReadOnlyList<EnemyDef> enemies,
        NewGameDef? newGame,
        List<string> errors)
    {
        skillMap = index(skills, "skill", s => s.id, errors);
        itemMap = index(items, "item", i => i.id, errors);
        characterMap = index(characters, "character", c => c.id, errors);
        enemyMap = index(enemies, "enemy", e => e.id, errors);
        this.newGame = newGame;
    }

    public IReadOnlyCollection<SkillDef> skills => skillMap.Values;
    public IReadOnlyCollection<ItemDef> items => itemMap.Values;
    public IReadOnlyCollection<CharacterDef> characters => characterMap.Values;
    public IReadOnlyCollection<EnemyDef> enemies => enemyMap.Values;
    public NewGameDef? newGame { get; }

    public SkillDef skill(string id) => lookup(skillMap, id, "skill");
    public ItemDef item(string id) => lookup(itemMap, id, "item");
    public CharacterDef character(string id) => lookup(characterMap, id, "character");
    public EnemyDef enemy(string id) => lookup(enemyMap, id, "enemy");

    public static ContentDb loadDirectory(string directory)
    {
        Dictionary<string, string> files = new();
        foreach (string name in new[] { skillsFile, itemsFile, charactersFile, enemiesFile, newGameFile })
        {
            string path = Path.Combine(directory, name);
            if (File.Exists(path))
            {
                files[name] = File.ReadAllText(path);
            }
        }

        return parse(files);
    }

    /// <summary>Builds a database from file name → JSON text. Throws <see cref="ContentException"/> listing every problem found.</summary>
    public static ContentDb parse(IReadOnlyDictionary<string, string> files)
    {
        List<string> errors = new();
        ContentDb db = new(
            readList<SkillDef>(files, skillsFile, errors),
            readList<ItemDef>(files, itemsFile, errors),
            readList<CharacterDef>(files, charactersFile, errors),
            readList<EnemyDef>(files, enemiesFile, errors),
            read<NewGameDef>(files, newGameFile, errors),
            errors);
        db.validate(errors);
        if (errors.Count > 0)
        {
            throw new ContentException(errors);
        }

        return db;
    }

    private static IReadOnlyList<T> readList<T>(IReadOnlyDictionary<string, string> files, string name, List<string> errors) =>
        read<List<T>>(files, name, errors) ?? new List<T>();

    private static T? read<T>(IReadOnlyDictionary<string, string> files, string name, List<string> errors)
        where T : class
    {
        if (!files.TryGetValue(name, out string? json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(json, jsonOptions);
        }
        catch (JsonException ex)
        {
            errors.Add($"{name}: {ex.Message}");
            return null;
        }
    }

    private static Dictionary<string, T> index<T>(IEnumerable<T> defs, string kind, Func<T, string> idOf, List<string> errors)
    {
        Dictionary<string, T> map = new();
        foreach (T def in defs)
        {
            if (!map.TryAdd(idOf(def), def))
            {
                errors.Add($"duplicate {kind} id '{idOf(def)}'");
            }
        }

        return map;
    }

    private static T lookup<T>(Dictionary<string, T> map, string id, string kind) =>
        map.TryGetValue(id, out T? def) ? def : throw new KeyNotFoundException($"unknown {kind} id '{id}'");

    private void validate(List<string> errors)
    {
        void requireSkill(string id, string owner)
        {
            if (!skillMap.ContainsKey(id))
            {
                errors.Add($"{owner}: unknown skill '{id}'");
            }
        }

        bool requireItem(string id, string owner)
        {
            if (itemMap.ContainsKey(id))
            {
                return true;
            }

            errors.Add($"{owner}: unknown item '{id}'");
            return false;
        }

        foreach (ItemDef item in itemMap.Values)
        {
            string owner = $"item '{item.id}'";
            if (item.kind == ItemKind.Equipment && item.slot is null)
            {
                errors.Add($"{owner}: equipment needs a slot");
            }

            if (item.kind == ItemKind.Consumable && item.effect is null)
            {
                errors.Add($"{owner}: consumable needs an effect");
            }

            if (item.price < 0)
            {
                errors.Add($"{owner}: negative price");
            }
        }

        foreach (CharacterDef character in characterMap.Values)
        {
            string owner = $"character '{character.id}'";
            foreach (SkillLearnDef learn in character.skills)
            {
                requireSkill(learn.skillId, owner);
            }

            HashSet<EquipSlot> usedSlots = new();
            foreach (string itemId in character.initialEquipment)
            {
                if (!requireItem(itemId, owner))
                {
                    continue;
                }

                ItemDef item = itemMap[itemId];
                if (item.kind != ItemKind.Equipment || item.slot is null)
                {
                    errors.Add($"{owner}: initial equipment '{itemId}' is not equipment");
                }
                else if (!usedSlots.Add(item.slot.Value))
                {
                    errors.Add($"{owner}: two initial items in slot {item.slot}");
                }
            }
        }

        foreach (EnemyDef enemy in enemyMap.Values)
        {
            string owner = $"enemy '{enemy.id}'";
            if (enemy.stats.maxHp <= 0)
            {
                errors.Add($"{owner}: maxHp must be positive");
            }

            foreach (EnemyActionDef action in enemy.actions)
            {
                if (action.skillId is not null)
                {
                    requireSkill(action.skillId, owner);
                }

                if (action.weight <= 0)
                {
                    errors.Add($"{owner}: action weights must be positive");
                }
            }

            foreach (DropDef drop in enemy.drops)
            {
                requireItem(drop.itemId, owner);
                if (drop.chance is < 0 or > 1)
                {
                    errors.Add($"{owner}: drop chance for '{drop.itemId}' must be within 0–1");
                }
            }
        }

        if (newGame is not null)
        {
            if (newGame.party.Count == 0)
            {
                errors.Add($"{newGameFile}: party is empty");
            }

            foreach (StartingMemberDef member in newGame.party)
            {
                if (!characterMap.ContainsKey(member.characterId))
                {
                    errors.Add($"{newGameFile}: unknown character '{member.characterId}'");
                }
            }

            foreach (ItemStackDef stack in newGame.items)
            {
                requireItem(stack.itemId, newGameFile);
            }
        }
    }
}
