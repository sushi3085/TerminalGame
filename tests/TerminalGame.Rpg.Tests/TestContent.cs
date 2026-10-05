using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Tests;

/// <summary>A small, fixed content set so tests do not break when the real game content is rebalanced.</summary>
internal static class TestContent
{
    public const string skills = """
        [
          { "id": "bolt", "name": "Bolt", "mpCost": 5, "target": "SingleEnemy", "effect": { "kind": "Magical", "power": 200 } },
          { "id": "quake", "name": "Quake", "mpCost": 8, "target": "AllEnemies", "effect": { "kind": "Magical", "power": 100 } },
          { "id": "cure", "name": "Cure", "mpCost": 3, "target": "SingleAlly", "effect": { "kind": "Heal", "power": 10, "magicScaling": 100 } },
          { "id": "late", "name": "Late", "mpCost": 1, "target": "Self", "effect": { "kind": "Heal", "power": 1 } }
        ]
        """;

    public const string items = """
        [
          { "id": "potion", "name": "Potion", "kind": "Consumable", "price": 10, "effect": { "kind": "Heal", "power": 30 } },
          { "id": "ether", "name": "Ether", "kind": "Consumable", "price": 20, "effect": { "kind": "RestoreMp", "power": 10 } },
          { "id": "stick", "name": "Stick", "kind": "Equipment", "slot": "Weapon", "bonus": { "attack": 2 } },
          { "id": "blade", "name": "Blade", "kind": "Equipment", "slot": "Weapon", "bonus": { "attack": 10, "speed": -1 } },
          { "id": "plate", "name": "Plate", "kind": "Equipment", "slot": "Body", "bonus": { "defense": 5, "maxHp": 20 } }
        ]
        """;

    public const string characters = """
        [
          {
            "id": "hero", "name": "Hero",
            "baseStats": { "maxHp": 50, "maxMp": 10, "attack": 10, "defense": 4, "magic": 8, "speed": 10 },
            "growth": { "maxHp": 10, "maxMp": 2, "attack": 2, "defense": 1, "magic": 1, "speed": 1 },
            "skills": [ { "skillId": "bolt" }, { "skillId": "cure", "level": 2 }, { "skillId": "late", "level": 3 }, { "skillId": "quake", "level": 5 } ],
            "initialEquipment": [ "stick" ]
          },
          {
            "id": "slowpoke", "name": "Slowpoke",
            "baseStats": { "maxHp": 80, "maxMp": 0, "attack": 8, "defense": 6, "magic": 0, "speed": 1 },
            "growth": { "maxHp": 12 }
          }
        ]
        """;

    public const string enemies = """
        [
          { "id": "blob", "name": "Blob", "stats": { "maxHp": 30, "attack": 9, "defense": 2, "speed": 5 }, "exp": 20, "gold": 7,
            "drops": [ { "itemId": "potion", "chance": 1 } ] },
          { "id": "wisp", "name": "Wisp", "stats": { "maxHp": 20, "maxMp": 10, "attack": 4, "magic": 10, "speed": 20 }, "exp": 15, "gold": 3,
            "actions": [ { "skillId": "bolt", "weight": 1 } ] },
          { "id": "titan", "name": "Titan", "stats": { "maxHp": 999, "attack": 200, "defense": 50, "speed": 1 }, "exp": 1000 }
        ]
        """;

    public const string newGame = """
        { "party": [ { "characterId": "hero", "level": 1 } ], "items": [ { "itemId": "potion", "count": 2 } ], "gold": 100 }
        """;

    public static Dictionary<string, string> files() => new()
    {
        [ContentDb.skillsFile] = skills,
        [ContentDb.itemsFile] = items,
        [ContentDb.charactersFile] = characters,
        [ContentDb.enemiesFile] = enemies,
        [ContentDb.newGameFile] = newGame,
    };

    public static ContentDb load() => ContentDb.parse(files());

    public static GameSession session(int seed = 1) => GameSession.newGame(load(), seed);
}
