using System.Text.Json;
using System.Text.Json.Serialization;
using TerminalGame.Rpg.Script;

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
/// (<c>skills.json</c>, <c>items.json</c>, <c>characters.json</c>, <c>enemies.json</c>, <c>locations.json</c>,
/// <c>shops.json</c>, <c>scripts.json</c>, <c>newGame.json</c>); every file is optional. Loading validates cross references, so a typo in an id fails at startup
/// instead of mid-game.
/// </summary>
public sealed class ContentDb
{
    public const string skillsFile = "skills.json";
    public const string itemsFile = "items.json";
    public const string charactersFile = "characters.json";
    public const string enemiesFile = "enemies.json";
    public const string locationsFile = "locations.json";
    public const string shopsFile = "shops.json";
    public const string scriptsFile = "scripts.json";
    public const string newGameFile = "newGame.json";

    private static readonly string[] allFiles =
        { skillsFile, itemsFile, charactersFile, enemiesFile, locationsFile, shopsFile, scriptsFile, newGameFile };

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
    private readonly Dictionary<string, LocationDef> locationMap;
    private readonly Dictionary<string, ShopDef> shopMap;
    private readonly Dictionary<string, IReadOnlyList<ScriptCommandDef>> scriptMap;

    private ContentDb(IReadOnlyDictionary<string, string> files, List<string> errors)
    {
        skillMap = index(readList<SkillDef>(files, skillsFile, errors), "skill", s => s.id, errors);
        itemMap = index(readList<ItemDef>(files, itemsFile, errors), "item", i => i.id, errors);
        characterMap = index(readList<CharacterDef>(files, charactersFile, errors), "character", c => c.id, errors);
        enemyMap = index(readList<EnemyDef>(files, enemiesFile, errors), "enemy", e => e.id, errors);
        locationMap = index(readList<LocationDef>(files, locationsFile, errors), "location", l => l.id, errors);
        shopMap = index(readList<ShopDef>(files, shopsFile, errors), "shop", s => s.id, errors);
        scriptMap = read<Dictionary<string, IReadOnlyList<ScriptCommandDef>>>(files, scriptsFile, errors) ?? new();
        newGame = read<NewGameDef>(files, newGameFile, errors);
    }

    public IReadOnlyCollection<SkillDef> skills => skillMap.Values;
    public IReadOnlyCollection<ItemDef> items => itemMap.Values;
    public IReadOnlyCollection<CharacterDef> characters => characterMap.Values;
    public IReadOnlyCollection<EnemyDef> enemies => enemyMap.Values;
    public IReadOnlyCollection<LocationDef> locations => locationMap.Values;
    public IReadOnlyCollection<ShopDef> shops => shopMap.Values;
    public IReadOnlyDictionary<string, IReadOnlyList<ScriptCommandDef>> scripts => scriptMap;
    public NewGameDef? newGame { get; }

    public SkillDef skill(string id) => lookup(skillMap, id, "skill");
    public ItemDef item(string id) => lookup(itemMap, id, "item");
    public CharacterDef character(string id) => lookup(characterMap, id, "character");
    public EnemyDef enemy(string id) => lookup(enemyMap, id, "enemy");
    public LocationDef location(string id) => lookup(locationMap, id, "location");
    public ShopDef shop(string id) => lookup(shopMap, id, "shop");
    public IReadOnlyList<ScriptCommandDef> script(string id) => lookup(scriptMap, id, "script");

    public bool hasItem(string id) => itemMap.ContainsKey(id);
    public bool hasCharacter(string id) => characterMap.ContainsKey(id);
    public bool hasLocation(string id) => locationMap.ContainsKey(id);

    public static ContentDb loadDirectory(string directory)
    {
        Dictionary<string, string> files = new();
        foreach (string name in allFiles)
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
        ContentDb db = new(files, errors);
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

            if (newGame.location is not null && !locationMap.ContainsKey(newGame.location))
            {
                errors.Add($"{newGameFile}: unknown location '{newGame.location}'");
            }
        }

        validateWorld(errors, requireItem);
    }

    private void validateWorld(List<string> errors, Func<string, string, bool> requireItem)
    {
        void requireScript(string id, string owner)
        {
            if (!scriptMap.ContainsKey(id))
            {
                errors.Add($"{owner}: unknown script '{id}'");
            }
        }

        void requireLocation(string id, string owner)
        {
            if (!locationMap.ContainsKey(id))
            {
                errors.Add($"{owner}: unknown location '{id}'");
            }
        }

        void requireEnemies(IEnumerable<string> ids, string owner)
        {
            foreach (string id in ids)
            {
                if (!enemyMap.ContainsKey(id))
                {
                    errors.Add($"{owner}: unknown enemy '{id}'");
                }
            }
        }

        void requireCondition(string? source, string owner)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return;
            }

            try
            {
                foreach ((string kind, string name) in Condition.parse(source).references)
                {
                    if (kind == "item")
                    {
                        requireItem(name, owner);
                    }
                    else if (kind is "party" or "level" && !characterMap.ContainsKey(name))
                    {
                        errors.Add($"{owner}: unknown character '{name}' in condition");
                    }
                }
            }
            catch (FormatException ex)
            {
                errors.Add($"{owner}: {ex.Message}");
            }
        }

        foreach (ShopDef shop in shopMap.Values)
        {
            foreach (string itemId in shop.items)
            {
                requireItem(itemId, $"shop '{shop.id}'");
            }
        }

        foreach (LocationDef location in locationMap.Values)
        {
            string owner = $"location '{location.id}'";
            foreach (ExitDef exit in location.exits)
            {
                requireLocation(exit.to, owner);
                requireCondition(exit.@if, owner);
            }

            foreach (SpotDef spot in location.spots)
            {
                requireScript(spot.script, owner);
                requireCondition(spot.@if, owner);
            }

            foreach (TriggerDef trigger in location.onEnter)
            {
                requireScript(trigger.script, owner);
                requireCondition(trigger.@if, owner);
            }

            foreach (EncounterDef encounter in location.encounters)
            {
                if (encounter.enemies.Count == 0)
                {
                    errors.Add($"{owner}: empty encounter");
                }

                requireEnemies(encounter.enemies, owner);
            }

            if (location.encounterRate is < 0 or > 1)
            {
                errors.Add($"{owner}: encounterRate must be within 0–1");
            }
        }

        void validateCommands(IReadOnlyList<ScriptCommandDef> commands, string owner)
        {
            foreach (ScriptCommandDef c in commands)
            {
                List<string> ops = c.operations.ToList();
                if (ops.Count != 1)
                {
                    errors.Add($"{owner}: each command needs exactly one operation, found [{string.Join(", ", ops)}]");
                    continue;
                }

                requireCondition(c.@if, owner);
                if (c.@if is not null && c.then is null && c.@else is null)
                {
                    errors.Add($"{owner}: 'if' without 'then' or 'else'");
                }

                if (c.then is not null)
                {
                    validateCommands(c.then, owner);
                }

                if (c.@else is not null)
                {
                    validateCommands(c.@else, owner);
                }

                foreach (ChoiceDef option in c.choice ?? [])
                {
                    requireCondition(option.@if, owner);
                    validateCommands(option.then, owner);
                }

                if (c.giveItem is not null)
                {
                    requireItem(c.giveItem, owner);
                }

                if (c.takeItem is not null)
                {
                    requireItem(c.takeItem, owner);
                }

                if (c.join is not null && !characterMap.ContainsKey(c.join))
                {
                    errors.Add($"{owner}: unknown character '{c.join}'");
                }

                if (c.battle is not null)
                {
                    if (c.battle.Count == 0)
                    {
                        errors.Add($"{owner}: battle without enemies");
                    }

                    requireEnemies(c.battle, owner);
                }

                if (c.shop is not null && !shopMap.ContainsKey(c.shop))
                {
                    errors.Add($"{owner}: unknown shop '{c.shop}'");
                }

                if (c.travel is not null)
                {
                    requireLocation(c.travel, owner);
                }

                if (c.run is not null)
                {
                    requireScript(c.run, owner);
                }
            }
        }

        foreach ((string id, IReadOnlyList<ScriptCommandDef> commands) in scriptMap)
        {
            validateCommands(commands, $"script '{id}'");
        }
    }
}
