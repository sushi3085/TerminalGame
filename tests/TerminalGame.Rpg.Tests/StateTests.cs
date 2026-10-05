using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.Tests;

public class StateTests
{
    [Fact]
    public void newGameFollowsDefinition()
    {
        GameSession session = TestContent.session();

        PartyMember hero = Assert.Single(session.party);
        Assert.Equal("Hero", hero.name);
        Assert.Equal(1, hero.level);
        Assert.Equal(2, session.inventory.count("potion"));
        Assert.Equal(100, session.gold);
        Assert.Equal("stick", hero.equippedIn(EquipSlot.Weapon)?.id);
    }

    [Fact]
    public void statsAreBasePlusGrowthPlusEquipment()
    {
        ContentDb db = TestContent.load();
        PartyMember hero = new(db, db.character("hero"), level: 3);

        StatBlock s = hero.stats;
        Assert.Equal(50 + 2 * 10, s.maxHp);
        Assert.Equal(10 + 2 * 2 + 2, s.attack); // + stick
        Assert.Equal(s.maxHp, hero.hp);
        Assert.Equal(s.maxMp, hero.mp);
    }

    [Fact]
    public void equippingSwapsAndReturnsPreviousItem()
    {
        ContentDb db = TestContent.load();
        PartyMember hero = new(db, db.character("hero"));

        ItemDef? previous = hero.equip(db.item("blade"));

        Assert.Equal("stick", previous?.id);
        Assert.Equal(10 + 10, hero.stats.attack);
        Assert.Equal(10 - 1, hero.stats.speed);
        Assert.Throws<ArgumentException>(() => hero.equip(db.item("potion")));
    }

    [Fact]
    public void removingMaxHpGearClampsCurrentHp()
    {
        ContentDb db = TestContent.load();
        PartyMember hero = new(db, db.character("hero"));
        hero.equip(db.item("plate"));
        hero.restore();
        Assert.Equal(70, hero.hp);

        hero.unequip(EquipSlot.Body);

        Assert.Equal(50, hero.hp);
    }

    [Fact]
    public void hpAndMpAreClamped()
    {
        GameSession session = TestContent.session();
        PartyMember hero = session.party[0];

        hero.hp = -5;
        Assert.Equal(0, hero.hp);
        Assert.False(hero.isAlive);
        hero.hp = 9999;
        Assert.Equal(hero.stats.maxHp, hero.hp);
    }

    [Fact]
    public void expCurveIsIncreasing()
    {
        for (int level = 1; level < Progression.maxLevel - 1; level++)
        {
            Assert.True(Progression.expToNext(level + 1) > Progression.expToNext(level));
        }

        Assert.Equal(0, Progression.expToNext(Progression.maxLevel));
    }

    [Fact]
    public void gainingExpLevelsUpRepeatedlyAndTeachesSkills()
    {
        ContentDb db = TestContent.load();
        PartyMember hero = new(db, db.character("hero"));
        hero.hp = 10;
        int needed = Progression.expToNext(1) + Progression.expToNext(2);

        IReadOnlyList<LevelUp> ups = hero.gainExp(needed + 3);

        Assert.Equal(new[] { 2, 3 }, ups.Select(u => u.newLevel));
        Assert.Equal("cure", Assert.Single(ups[0].learnedSkills).id);
        Assert.Equal("late", Assert.Single(ups[1].learnedSkills).id);
        Assert.Equal(3, hero.level);
        Assert.Equal(3, hero.exp);
        Assert.Equal(10 + 2 * 10, hero.hp); // max HP gains are added to current HP
        Assert.Equal(new[] { "bolt", "cure", "late" }, hero.skills.Select(s => s.id));
    }

    [Fact]
    public void levelCapStopsExp()
    {
        ContentDb db = TestContent.load();
        PartyMember hero = new(db, db.character("hero"), Progression.maxLevel);

        Assert.Empty(hero.gainExp(1_000_000));
        Assert.Equal(0, hero.exp);
    }

    [Fact]
    public void inventoryKeepsAcquisitionOrderAndCaps()
    {
        Inventory inventory = new();
        inventory.add("b");
        inventory.add("a", 3);
        inventory.add("b", 2);

        Assert.Equal(new[] { ("b", 3), ("a", 3) }, inventory.entries);
        Assert.False(inventory.remove("a", 4));
        Assert.True(inventory.remove("b", 3));
        Assert.Equal(new[] { ("a", 3) }, inventory.entries);
        Assert.Equal(Inventory.maxStack - 3, inventory.add("a", 500));
        Assert.Equal(Inventory.maxStack, inventory.count("a"));
    }

    [Fact]
    public void flagsDefaultToZero()
    {
        Flags flags = new();
        Assert.Equal(0, flags["x"]);
        flags["x"] = 2;
        Assert.True(flags.isSet("x"));
        flags["x"] = 0;
        Assert.False(flags.isSet("x"));
    }

    [Fact]
    public void partyLimits()
    {
        GameSession session = TestContent.session();
        Assert.Throws<InvalidOperationException>(() => session.addMember("hero"));
        session.addMember("slowpoke");
        Assert.Equal(2, session.party.Count);
    }
}
