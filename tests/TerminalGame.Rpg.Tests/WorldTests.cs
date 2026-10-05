using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;

namespace TerminalGame.Rpg.Tests;

public class WorldTests
{
    [Fact]
    public void exitsAndSpotsFollowFlags()
    {
        GameSession session = TestContent.session();
        LocationDef town = session.currentLocation();

        Assert.Equal("town", town.id);
        Assert.Equal(new[] { "前往Woods" }, session.visibleExits(town).Select(session.exitLabel));
        Assert.Equal(new[] { "Elder" }, session.visibleSpots(town).Select(s => s.label));
        Assert.Equal("elder", session.arrivalScript(town));

        session.flags["caveOpen"] = 1;
        session.flags["metElder"] = 1;

        Assert.Equal(new[] { "前往Woods", "Into the cave" }, session.visibleExits(town).Select(session.exitLabel));
        Assert.Equal(2, session.visibleSpots(town).Count);
        Assert.Null(session.arrivalScript(town));
    }

    [Fact]
    public void travelMovesTheParty()
    {
        GameSession session = TestContent.session();

        LocationDef woods = session.travel(session.currentLocation().exits[0]);

        Assert.Equal("woods", woods.id);
        Assert.Equal("woods", session.locationId);
    }

    [Fact]
    public void townsNeverHaveEncounters()
    {
        GameSession session = TestContent.session();

        Assert.Null(session.rollEncounter(session.currentLocation(), forced: true));
    }

    [Fact]
    public void encounterRateAndWeightsRoughlyHold()
    {
        GameSession session = TestContent.session(seed: 9);
        LocationDef woods = session.content.location("woods");
        int battles = 0;
        int pairs = 0;
        const int trials = 4000;

        for (int i = 0; i < trials; i++)
        {
            EncounterDef? encounter = session.rollEncounter(woods);
            if (encounter is not null)
            {
                battles++;
                pairs += encounter.enemies.Count == 2 ? 1 : 0;
            }
        }

        Assert.InRange(battles / (double)trials, 0.45, 0.55);   // encounterRate 0.5
        Assert.InRange(pairs / (double)battles, 0.20, 0.30);    // weight 1 of 4
        Assert.NotNull(session.rollEncounter(woods, forced: true));
    }

    [Fact]
    public void buyingAndSelling()
    {
        GameSession session = TestContent.session();
        ItemDef potion = session.content.item("potion");
        ItemDef blade = session.content.item("blade");

        Assert.Equal(10, ShopRules.maxAffordable(session, potion));
        Assert.False(ShopRules.buy(session, potion, 11));
        Assert.True(ShopRules.buy(session, potion, 3));
        Assert.Equal(70, session.gold);
        Assert.Equal(5, session.inventory.count("potion"));

        Assert.True(ShopRules.sell(session, potion, 5));
        Assert.Equal(70 + 5 * 5, session.gold);
        Assert.False(ShopRules.sell(session, potion));
        Assert.Equal(0, ShopRules.maxAffordable(session, blade)); // price 0 means "not for sale"
    }

    [Fact]
    public void equippingFromInventorySwapsTheOldPieceBack()
    {
        GameSession session = TestContent.session();
        PartyMember hero = session.party[0];
        ItemDef blade = session.content.item("blade");

        Assert.False(session.equipFromInventory(hero, blade));
        session.inventory.add("blade");

        Assert.True(session.equipFromInventory(hero, blade));
        Assert.Equal("blade", hero.equippedIn(EquipSlot.Weapon)?.id);
        Assert.Equal(1, session.inventory.count("stick"));
        Assert.Equal(0, session.inventory.count("blade"));
        Assert.False(session.equipFromInventory(hero, session.content.item("potion")));
        Assert.Equal(2, session.inventory.count("potion"));

        session.unequipToInventory(hero, EquipSlot.Weapon);
        Assert.Null(hero.equippedIn(EquipSlot.Weapon));
        Assert.Equal(1, session.inventory.count("blade"));
    }

    [Fact]
    public void defeatCostsHalfTheGoldAndReturnsToRespawn()
    {
        GameSession session = TestContent.session();
        session.locationId = "woods";
        session.party[0].hp = 0;
        session.gold = 75;

        int lost = session.applyDefeat();

        Assert.Equal(37, lost);
        Assert.Equal(38, session.gold);
        Assert.Equal("town", session.locationId);
        Assert.Equal(session.party[0].stats.maxHp, session.party[0].hp);
    }

    [Fact]
    public void locationValidation()
    {
        Dictionary<string, string> files = TestContent.files();
        files[ContentDb.locationsFile] = """
            [ { "id": "town", "name": "T", "encounterRate": 2,
                "exits": [ { "to": "void", "if": "party:nobody" } ],
                "spots": [ { "label": "x", "script": "missing" } ],
                "encounters": [ { "enemies": [] }, { "enemies": [ "dragon" ] } ] } ]
            """;

        ContentException ex = Assert.Throws<ContentException>(() => ContentDb.parse(files));

        string all = string.Join("\n", ex.errors);
        Assert.Contains("unknown location 'void'", all);
        Assert.Contains("unknown character 'nobody' in condition", all);
        Assert.Contains("unknown script 'missing'", all);
        Assert.Contains("empty encounter", all);
        Assert.Contains("unknown enemy 'dragon'", all);
        Assert.Contains("encounterRate", all);
        Assert.Contains("script 'ambush': unknown location 'cave'", all); // scripts are checked against the new location list too
    }
}

public class SaveTests
{
    [Fact]
    public void roundTripsEverything()
    {
        GameSession session = TestContent.session();
        session.addMember("slowpoke", 4);
        session.party[0].gainExp(30);
        session.party[0].hp = 7;
        session.inventory.add("blade");
        session.equipFromInventory(session.party[0], session.content.item("blade"));
        session.inventory.add("ether", 3);
        session.flags["metElder"] = 1;
        session.flags["quizzes"] = 4;
        session.gold = 321;
        session.locationId = "woods";
        session.respawnLocationId = "town";
        session.playSeconds = 3725;

        string json = SaveSystem.serialize(SaveSystem.capture(session));
        GameSession loaded = SaveSystem.restore(session.content, SaveSystem.deserialize(json));

        Assert.Equal(session.party.Select(describe), loaded.party.Select(describe));
        Assert.Equal(session.inventory.entries, loaded.inventory.entries);
        Assert.Equal(session.flags.all, loaded.flags.all);
        Assert.Equal((321, "woods", "town", 3725.0), (loaded.gold, loaded.locationId, loaded.respawnLocationId, loaded.playSeconds));
        Assert.Contains("\"Weapon\": \"blade\"", json); // readable enums
    }

    private static string describe(PartyMember m) =>
        $"{m.def.id} L{m.level} exp{m.exp} hp{m.hp} mp{m.mp} {string.Join(",", m.equipment.OrderBy(e => e.Key).Select(e => $"{e.Key}={e.Value}"))}";

    [Fact]
    public void unknownItemsAreDroppedButUnknownCharactersFail()
    {
        ContentDb content = TestContent.load();
        SaveData data = new()
        {
            party = [new SavedMember { characterId = "hero", level = 2, hp = 999, equipment = { [EquipSlot.Weapon] = "removedSword", [EquipSlot.Body] = "stick" } }],
            inventory = [new ItemStackDef { itemId = "removedPotion", count = 2 }, new ItemStackDef { itemId = "potion", count = 1 }],
            locationId = "removedPlace",
        };

        GameSession loaded = SaveSystem.restore(content, data);

        Assert.Empty(loaded.party[0].equipment); // unknown id, and stick is not body armor
        Assert.Equal(loaded.party[0].stats.maxHp, loaded.party[0].hp);
        Assert.Equal(new[] { ("potion", 1) }, loaded.inventory.entries);
        Assert.Equal("town", loaded.locationId); // falls back to the start

        SaveData broken = data with { party = [new SavedMember { characterId = "deletedHero" }] };
        Assert.Throws<SaveException>(() => SaveSystem.restore(content, broken));
        Assert.Throws<SaveException>(() => SaveSystem.restore(content, data with { version = 99 }));
        Assert.Throws<SaveException>(() => SaveSystem.deserialize("{ not json"));
    }

    [Fact]
    public void storeWritesSlotsAtomically()
    {
        string directory = Path.Combine(Path.GetTempPath(), "tg-save-" + Guid.NewGuid().ToString("N"));
        try
        {
            SaveStore store = new(directory);
            GameSession session = TestContent.session();
            session.gold = 42;

            Assert.Null(store.peek(1));
            store.save(session, 1);

            Assert.True(store.exists(1));
            Assert.False(File.Exists(store.pathFor(1) + ".tmp"));
            Assert.Equal(42, store.load(session.content, 1).gold);
            Assert.StartsWith("Hero Lv.1  Town  00:00", store.peek(1)!.summary(session.content));

            File.WriteAllText(store.pathFor(2), "garbage");
            Assert.Null(store.peek(2));
            Assert.Throws<SaveException>(() => store.load(session.content, 2));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
