using TerminalGame.Rpg.Data;

namespace TerminalGame.Rpg.Tests;

public class ContentTests
{
    [Fact]
    public void shippedContentLoadsAndValidates()
    {
        ContentDb db = ContentDb.loadDirectory(Path.Combine(AppContext.BaseDirectory, "content"));

        Assert.NotEmpty(db.characters);
        Assert.NotEmpty(db.enemies);
        Assert.NotNull(db.newGame);
    }

    [Fact]
    public void parsesDefinitions()
    {
        ContentDb db = TestContent.load();

        SkillDef bolt = db.skill("bolt");
        Assert.Equal(5, bolt.mpCost);
        Assert.Equal(TargetKind.SingleEnemy, bolt.target);
        Assert.Equal(EffectKind.Magical, bolt.effect.kind);

        ItemDef blade = db.item("blade");
        Assert.Equal(EquipSlot.Weapon, blade.slot);
        Assert.Equal(10, blade.bonus.attack);
        Assert.Equal(-1, blade.bonus.speed);
        Assert.Equal(0, blade.bonus.defense);

        Assert.Equal(3, db.enemies.Count);
        Assert.Throws<KeyNotFoundException>(() => db.enemy("dragon"));
    }

    [Fact]
    public void reportsEveryBrokenReference()
    {
        Dictionary<string, string> files = TestContent.files();
        files[ContentDb.enemiesFile] = """
            [ { "id": "bad", "name": "Bad", "stats": { "maxHp": 1 },
                "actions": [ { "skillId": "nope" } ], "drops": [ { "itemId": "ghostItem", "chance": 2 } ] } ]
            """;
        files[ContentDb.newGameFile] = """{ "party": [ { "characterId": "nobody" } ] }""";

        ContentException ex = Assert.Throws<ContentException>(() => ContentDb.parse(files));

        Assert.Contains(ex.errors, e => e.Contains("unknown skill 'nope'"));
        Assert.Contains(ex.errors, e => e.Contains("unknown item 'ghostItem'"));
        Assert.Contains(ex.errors, e => e.Contains("drop chance"));
        Assert.Contains(ex.errors, e => e.Contains("unknown character 'nobody'"));
    }

    [Fact]
    public void rejectsDuplicateIdsAndMalformedItems()
    {
        Dictionary<string, string> files = TestContent.files();
        files[ContentDb.itemsFile] = """
            [
              { "id": "potion", "name": "A", "kind": "Consumable", "effect": { "kind": "Heal", "power": 1 } },
              { "id": "potion", "name": "B", "kind": "Consumable", "effect": { "kind": "Heal", "power": 1 } },
              { "id": "ether", "name": "No effect", "kind": "Consumable" },
              { "id": "stick", "name": "No slot", "kind": "Equipment" },
              { "id": "blade", "name": "Blade", "kind": "Equipment", "slot": "Weapon" },
              { "id": "plate", "name": "Plate", "kind": "Equipment", "slot": "Body" }
            ]
            """;

        ContentException ex = Assert.Throws<ContentException>(() => ContentDb.parse(files));

        Assert.Contains(ex.errors, e => e.Contains("duplicate item id 'potion'"));
        Assert.Contains(ex.errors, e => e.Contains("'ether': consumable needs an effect"));
        Assert.Contains(ex.errors, e => e.Contains("'stick': equipment needs a slot"));
    }

    [Fact]
    public void missingRequiredFieldIsAnError()
    {
        Dictionary<string, string> files = TestContent.files();
        files[ContentDb.skillsFile] = """[ { "id": "x", "target": "Self", "effect": { "kind": "Heal", "power": 1 } } ]""";

        ContentException ex = Assert.Throws<ContentException>(() => ContentDb.parse(files));

        Assert.Contains(ex.errors, e => e.StartsWith(ContentDb.skillsFile));
    }

    [Fact]
    public void filesAreOptional()
    {
        ContentDb db = ContentDb.parse(new Dictionary<string, string>());

        Assert.Empty(db.items);
        Assert.Null(db.newGame);
    }
}
