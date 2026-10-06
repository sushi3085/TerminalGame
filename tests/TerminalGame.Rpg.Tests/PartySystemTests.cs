using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;

namespace TerminalGame.Rpg.Tests;

/// <summary>Revive, taunt, two-phase bosses and per-character equipment.</summary>
public class PartySystemTests
{
    private const string skills = """
        [
          { "id": "raise", "name": "Raise", "mpCost": 5, "target": "SingleAlly", "effect": { "kind": "Revive", "power": 50 } },
          { "id": "provoke", "name": "Provoke", "target": "Self", "effect": { "kind": "Status", "status": { "kind": "Taunt", "turns": 3 } } }
        ]
        """;

    private const string items = """
        [
          { "id": "feather", "name": "Feather", "kind": "Consumable", "effect": { "kind": "Revive", "power": 25 } },
          { "id": "axe", "name": "Axe", "kind": "Equipment", "slot": "Weapon", "bonus": { "attack": 9 }, "equippableBy": [ "tank" ] },
          { "id": "ring", "name": "Ring", "kind": "Equipment", "slot": "Accessory", "bonus": { "speed": 1 } }
        ]
        """;

    private const string characters = """
        [
          { "id": "cleric", "name": "Cleric", "baseStats": { "maxHp": 100, "maxMp": 30, "attack": 50, "speed": 20 }, "growth": {},
            "skills": [ { "skillId": "raise" } ] },
          { "id": "tank", "name": "Tank", "baseStats": { "maxHp": 200, "attack": 5, "defense": 10, "speed": 15 }, "growth": {},
            "skills": [ { "skillId": "provoke" } ] },
          { "id": "mage", "name": "Mage", "baseStats": { "maxHp": 40, "speed": 1 }, "growth": {} }
        ]
        """;

    private const string enemies = """
        [
          { "id": "brute", "name": "Brute", "stats": { "maxHp": 999, "attack": 30, "speed": 10 } },
          { "id": "kingA", "name": "King", "stats": { "maxHp": 10, "speed": 1 }, "exp": 100, "gold": 10,
            "nextPhase": "kingB", "phaseMessage": "The king transforms!" },
          { "id": "kingB", "name": "True King", "stats": { "maxHp": 20, "speed": 1 }, "exp": 200, "gold": 20 }
        ]
        """;

    private static Dictionary<string, string> files() => new()
    {
        [ContentDb.skillsFile] = skills,
        [ContentDb.itemsFile] = items,
        [ContentDb.charactersFile] = characters,
        [ContentDb.enemiesFile] = enemies,
    };

    private static GameSession party(params string[] members)
    {
        GameSession session = new(ContentDb.parse(files()), 3);
        foreach (string id in members)
        {
            session.addMember(id);
        }

        return session;
    }

    private static void finishEnemyTurns(BattleEngine engine)
    {
        while (engine.outcome == BattleOutcome.Ongoing && engine.currentActor!.side == Side.Enemies)
        {
            engine.execute(engine.decideEnemyAction());
        }
    }

    [Fact]
    public void reviveOnlyTargetsTheFallenAndRestoresAShareOfMaxHp()
    {
        GameSession session = party("cleric", "mage");
        session.party[1].hp = 0;
        BattleEngine engine = new(session, [session.content.enemy("brute")]);
        engine.start();
        Combatant cleric = engine.currentActor!;
        Combatant mage = engine.party[1];
        SkillDef raise = session.content.skill("raise");

        Assert.Equal([mage], engine.candidatesFor(cleric, raise.target, raise.effect));
        IReadOnlyList<BattleEvent> events = engine.execute(new SkillAction(cleric, raise, null));

        Assert.Contains(new ReviveEvent(mage, 20), events);
        Assert.True(mage.isAlive);
        Assert.Empty(engine.candidatesFor(cleric, raise.target, raise.effect));
    }

    [Fact]
    public void autoPolicyRevivesBeforeAnythingElse()
    {
        GameSession session = party("cleric", "mage");
        session.party[1].hp = 0;
        BattleEngine engine = new(session, [session.content.enemy("brute")]);
        engine.start();

        BattleAction action = new AutoPolicy().decide(engine, session);

        Assert.Equal("raise", Assert.IsType<SkillAction>(action).skill.id);
    }

    [Fact]
    public void reviveItemsWorkInTheField()
    {
        GameSession session = party("cleric", "mage");
        PartyMember mage = session.party[1];
        mage.hp = 0;
        session.inventory.add("feather");
        ItemDef feather = session.content.item("feather");

        Assert.True(FieldRules.usableInField(feather));
        Assert.False(FieldRules.wouldHelp(feather.effect!, session.party[0]));
        Assert.True(FieldRules.wouldHelp(feather.effect!, mage));

        IReadOnlyList<(PartyMember member, int amount)> result = FieldRules.useItem(session, feather, mage);

        Assert.Equal([(mage, 10)], result);
        Assert.Equal(10, mage.hp);
    }

    [Fact]
    public void tauntDrawsEnemyAttacks()
    {
        GameSession session = party("cleric", "tank", "mage");
        BattleEngine engine = new(session, [session.content.enemy("brute")]);
        engine.start();
        engine.execute(new GuardAction(engine.currentActor!)); // cleric (speed 20)
        Combatant tank = engine.currentActor!;
        engine.execute(new SkillAction(tank, session.content.skill("provoke"), null));
        Assert.True(tank.has(StatusKind.Taunt));

        for (int i = 0; i < 20 && tank.has(StatusKind.Taunt); i++)
        {
            BattleAction attack = engine.currentActor!.side == Side.Enemies ? engine.decideEnemyAction() : new GuardAction(engine.currentActor!);
            if (attack is AttackAction a)
            {
                Assert.Same(tank, a.target);
            }

            engine.execute(attack);
        }
    }

    [Fact]
    public void aFallenBossCanRiseInItsNextPhaseAndBothPayOut()
    {
        GameSession session = party("cleric");
        BattleEngine engine = new(session, [session.content.enemy("kingA")]);
        engine.start();
        Combatant cleric = engine.currentActor!;

        IReadOnlyList<BattleEvent> first = engine.execute(new AttackAction(cleric, engine.enemies[0]));

        PhaseChangedEvent change = Assert.Single(first.OfType<PhaseChangedEvent>());
        Assert.Equal("kingB", change.next.def.id);
        Assert.Same(change.next, engine.enemies[0]);
        Assert.Equal(BattleOutcome.Ongoing, engine.outcome);

        finishEnemyTurns(engine);
        IReadOnlyList<BattleEvent> second = engine.execute(new AttackAction(engine.currentActor!, engine.enemies[0]));

        VictoryEvent victory = Assert.Single(second.OfType<VictoryEvent>());
        Assert.Equal(300, victory.exp);
        Assert.Equal(30, victory.gold);
    }

    [Fact]
    public void equipmentCanBeLimitedToSomeCharacters()
    {
        GameSession session = party("cleric", "tank");
        ItemDef axe = session.content.item("axe");
        session.inventory.add("axe");

        Assert.False(session.equipFromInventory(session.party[0], axe));
        Assert.Throws<ArgumentException>(() => session.party[0].equip(axe));
        Assert.True(session.equipFromInventory(session.party[1], axe));
        Assert.True(session.content.item("ring").canBeEquippedBy(session.party[0].def));
    }

    [Fact]
    public void savesDropGearTheWearerCanNoLongerUse()
    {
        GameSession session = party("cleric");
        SaveData data = SaveSystem.capture(session) with
        {
            party = [new SavedMember { characterId = "cleric", hp = 50, equipment = new() { [EquipSlot.Weapon] = "axe" } }],
        };

        GameSession loaded = SaveSystem.restore(session.content, data);

        Assert.Null(loaded.party[0].equippedIn(EquipSlot.Weapon));
    }

    [Fact]
    public void brokenReferencesAreReported()
    {
        Dictionary<string, string> broken = files();
        broken[ContentDb.itemsFile] = """[ { "id": "axe", "name": "Axe", "kind": "Equipment", "slot": "Weapon", "equippableBy": [ "ghost" ] } ]""";
        broken[ContentDb.enemiesFile] = """[ { "id": "kingA", "name": "King", "stats": { "maxHp": 10 }, "nextPhase": "nobody" } ]""";
        broken[ContentDb.charactersFile] = """
            [ { "id": "mage", "name": "Mage", "baseStats": { "maxHp": 1 }, "growth": {}, "initialEquipment": [ "axe" ] } ]
            """;
        broken[ContentDb.skillsFile] = "[]";

        ContentException ex = Assert.Throws<ContentException>(() => ContentDb.parse(broken));

        Assert.Contains(ex.errors, e => e.Contains("unknown character 'ghost' in equippableBy"));
        Assert.Contains(ex.errors, e => e.Contains("unknown nextPhase 'nobody'"));
        Assert.Contains(ex.errors, e => e.Contains("cannot equip initial item 'axe'"));
    }
}
