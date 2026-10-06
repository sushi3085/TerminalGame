using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Tui.Backends;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo.Tests;

/// <summary>Drives the real scenes with key presses on an in-memory terminal, using the shipped content.</summary>
public sealed class GameFlowTests : IDisposable
{
    private sealed class EmptyScene : Scene
    {
        public EmptyScene() => root = new Spacer();
    }

    private readonly HeadlessBackend backend = new(80, 24);
    private readonly Application app;
    private readonly string saveDirectory = Path.Combine(Path.GetTempPath(), "tg-flow-" + Guid.NewGuid().ToString("N"));
    private readonly Game game;

    public GameFlowTests()
    {
        app = new Application(backend);
        ContentDb content = ContentDb.loadDirectory(Path.Combine(AppContext.BaseDirectory, "content"));
        game = new Game(content, new SaveStore(saveDirectory), seed: 3);
    }

    public void Dispose()
    {
        if (Directory.Exists(saveDirectory))
        {
            Directory.Delete(saveDirectory, recursive: true);
        }
    }

    private string screen => backend.screenText;

    /// <summary>Lets typewriter text finish.</summary>
    private void pump()
    {
        for (int i = 0; i < 40; i++)
        {
            app.step(0.25);
        }
    }

    private void press(params Key[] keys)
    {
        foreach (Key key in keys)
        {
            backend.queueKeys(key);
            app.step(0);
            pump();
        }
    }

    /// <summary>Presses Enter until <paramref name="done"/> holds; returns every screen seen on the way.</summary>
    private List<string> pressEnterUntil(Func<bool> done, int maxPresses = 200)
    {
        List<string> screens = new();
        app.step(0);
        pump();
        screens.Add(screen);
        for (int i = 0; !done(); i++)
        {
            Assert.True(i < maxPresses, "gave up; last screen:\n" + screen);
            press(Key.Enter);
            screens.Add(screen);
        }

        return screens;
    }

    private List<string> pressEnterUntil(string text) => pressEnterUntil(() => screen.Contains(text));

    /// <summary>True when a location's action menu is waiting for input (no message, popup or other scene on top).</summary>
    private bool atActions() => app.currentScene is LocationScene scene && scene.focusedWidget is MenuList && !scene.hasModal;

    private List<string> pressEnterUntilActions(string selected) => pressEnterUntil(() => atActions() && screen.Contains("▶ " + selected));

    /// <summary>Starts at <paramref name="locationId"/> with the intro already seen.</summary>
    private GameSession startAt(string locationId, params string[] flags)
    {
        GameSession session = game.startNew();
        session.flags["introDone"] = 1;
        foreach (string flag in flags)
        {
            session.flags[flag] = 1;
        }

        session.locationId = locationId;
        app.pushScene(new LocationScene(game));
        return session;
    }

    [Fact]
    public void newGamePlaysTheIntroThenShowsTheTownMenu()
    {
        app.pushScene(new TitleScene(game));

        List<string> screens = pressEnterUntilActions("長老家");

        Assert.Contains(screens, s => s.Contains("低語森林"));
        Assert.Contains(screens, s => s.Contains("獲得了傷藥 x3"));
        Assert.Equal(3, game.session.inventory.count("potion"));
        Assert.Equal(1, game.session.flags["introDone"]);
        Assert.IsType<LocationScene>(app.currentScene);
    }

    [Fact]
    public void exploringTheWoodsLeadsToABattleAndBack()
    {
        GameSession session = startAt("woodsEdge");
        session.party[0].gainExp(3000); // strong enough that "attack every turn" always wins
        pressEnterUntilActions("調查樹樁");

        press(Key.Down, Key.Down, Key.Down, Key.Enter); // 調查樹樁, 回晨曦鎮, 沿小徑深入, [四處探索]
        List<string> screens = pressEnterUntilActions("四處探索");

        Assert.Contains(screens, s => s.Contains("遭遇戰"));
        Assert.Contains(screens, s => s.Contains("戰鬥勝利"));
        Assert.True(session.gold > 50);
        Assert.Equal("woodsEdge", ((LocationScene)app.currentScene!).place.id);
    }

    [Fact]
    public void losingSendsThePartyBackToTownForHalfTheGold()
    {
        GameSession session = startAt("woodsEdge");
        pressEnterUntilActions("調查樹樁");
        session.party[0].hp = 1;
        session.gold = 80;

        press(Key.Down, Key.Down, Key.Down);
        List<string> screens = pressEnterUntilActions("長老家");

        Assert.Contains(screens, s => s.Contains("全員倒下了"));
        Assert.Contains(screens, s => s.Contains("失去了 40 G"));
        Assert.Equal("dawnTown", session.locationId);
        Assert.Equal(40, session.gold);
        Assert.Equal(session.party[0].stats.maxHp, session.party[0].hp);
    }

    [Fact]
    public void restingAtTheInnHealsMovesRespawnAndSaves()
    {
        GameSession session = startAt("dawnTown");
        pressEnterUntilActions("長老家");
        session.party[0].hp = 3;

        press(Key.Down, Key.Down, Key.Down); // 旅館
        pressEnterUntil("住宿 8 G？");
        press(Key.Enter); // 是
        pressEnterUntil("要記錄冒險嗎？");
        press(Key.Enter); // 是
        pressEnterUntilActions("旅館");

        Assert.Equal(42, session.gold);
        Assert.Equal(session.party[0].stats.maxHp, session.party[0].hp);
        Assert.True(game.hasSave);

        GameSession loaded = game.loadSaved();
        Assert.Equal(42, loaded.gold);
        Assert.Equal("dawnTown", loaded.respawnLocationId);
        Assert.Equal(3, loaded.party[0].level);
    }

    [Fact]
    public void titleOffersContinueOnlyWithASave()
    {
        app.pushScene(new TitleScene(game));
        app.step(0);
        Assert.DoesNotContain("艾倫 Lv.3", screen);

        game.startNew().gold = 77;
        game.save();
        app.replaceScene(new TitleScene(game));
        app.step(0);
        Assert.Contains("艾倫 Lv.3", screen);

        press(Key.Down, Key.Enter); // 繼續冒險
        Assert.IsType<LocationScene>(app.currentScene);
        Assert.Equal(77, game.session.gold);
    }

    [Fact]
    public void shopSellsOneItemPerConfirm()
    {
        GameSession session = game.startNew();
        app.pushScene(new EmptyScene());
        app.pushScene(new ShopScene(game, game.content.shop("dawnItems")));
        app.step(0);

        press(Key.Enter); // 買東西
        Assert.Contains("恢復 25 點 HP", screen);
        press(Key.Enter, Key.Enter); // two potions

        Assert.Equal(50 - 2 * 8, session.gold);
        Assert.Equal(2, session.inventory.count("potion"));
        Assert.Contains("持有金幣 34 G", screen);

        press(Key.Escape, Key.Down, Key.Enter, Key.Enter); // back, 賣東西, sell a potion
        Assert.Equal(34 + 4, session.gold);
        Assert.Equal(1, session.inventory.count("potion"));
    }

    [Fact]
    public void equipmentScreenPreviewsAndSwaps()
    {
        GameSession session = game.startNew();
        session.inventory.add("bronzeSword");
        app.pushScene(new EmptyScene());
        app.pushScene(new EquipScene(game));
        app.step(0);
        int attack = session.party[0].stats.attack;

        press(Key.Enter); // weapon slot → candidates, bronze sword highlighted
        Assert.Contains($"攻擊 {attack,3} → {attack + 4,3}", screen);
        press(Key.Enter);

        Assert.Equal("bronzeSword", session.party[0].equippedIn(EquipSlot.Weapon)?.id);
        Assert.Equal(1, session.inventory.count("woodenSword"));
        Assert.Equal(attack + 4, session.party[0].stats.attack);
    }

    [Fact]
    public void rescuingRinAddsHerToTheParty()
    {
        GameSession session = startAt("woodsClearing", "sawTracks");
        session.party[0].gainExp(3000);

        List<string> screens = pressEnterUntil(() => atActions() && screen.Contains("隊伍閒聊"));

        Assert.Contains(screens, s => s.Contains("琳加入了隊伍"));
        Assert.Contains(screens, s => s.Contains("野狼 A") && s.Contains("野狼 B"));
        Assert.Equal(new[] { "hero", "rin" }, session.party.Select(m => m.def.id));
        Assert.Equal(1, session.flags["metRin"]);
        Assert.Contains("前往森林深處", screen);
    }

    /// <summary>Waits for the action menu (pressing Enter through messages and battles), then picks <paramref name="label"/>.</summary>
    private void act(string label)
    {
        pressEnterUntil(atActions);
        for (int i = 0; i < 12 && !screen.Contains("▶ " + label); i++)
        {
            press(Key.Down);
        }

        Assert.True(screen.Contains("▶ " + label), $"no action '{label}' here:\n{screen}");
        press(Key.Enter);
    }

    [Fact]
    public void chapterOneCanBePlayedToTheEnd()
    {
        app.pushScene(new TitleScene(game));
        press(Key.Enter); // new game
        pressEnterUntil(atActions);
        GameSession session = game.session;
        session.party[0].gainExp(8000); // an "always attack" bot needs a big margin; this test is about the story wiring, not balance

        act("前往低語森林・入口");
        act("沿小徑深入");
        act("前往林間空地");
        pressEnterUntil(atActions); // tracks → Rin's rescue → wolves
        Assert.Equal(1, session.flags["sawTracks"]);
        Assert.Equal(1, session.flags["metRin"]);
        session.party[1].gainExp(8000);

        act("前往森林深處");
        act("調查苔蘚下的箱子");
        act("走向發光的蘑菇圈");
        List<string> bossFight = pressEnterUntil(atActions);
        Assert.Contains(bossFight, s => s.Contains("森林之主"));
        Assert.Equal(1, session.flags["bossDefeated"]);
        Assert.True(session.inventory.has("forestHeart"));
        Assert.True(session.inventory.has("longBow"));

        act("回林間空地");
        act("回到小徑");
        act("回森林入口");
        act("回晨曦鎮");
        List<string> ending = pressEnterUntil(atActions);

        Assert.Contains(ending, s => s.Contains("第一章「低語森林」 完"));
        Assert.Equal(1, session.flags["chapter1Done"]);
        Assert.Equal("dawnTown", session.locationId);
    }

    [Fact]
    public void chapterTwoCanBePlayedToTheEnd()
    {
        GameSession session = startAt("dawnTown", "metRin", "bossDefeated", "chapter1Done");
        session.addMember("rin", 8);
        foreach (PartyMember member in session.party)
        {
            member.gainExp(60_000);
        }

        act("往南方大道");
        act("前往河港村");
        pressEnterUntil(atActions); // the harbor master's story
        Assert.Equal(1, session.flags["portIntro"]);

        act("往河口沙洲");
        List<string> rescue = pressEnterUntil(atActions);
        Assert.Contains(rescue, s => s.Contains("巴爾加入了隊伍"));
        Assert.Equal(new[] { "hero", "rin", "bal" }, session.party.Select(m => m.def.id));
        Assert.Equal(session.party[0].level, session.party[2].level); // joins at the party's level

        act("進入沉船洞窟");
        act("往船艙深處");
        Assert.DoesNotContain("走下樓梯", screen);
        act("往側艙");
        act("轉動生鏽的絞盤");
        act("翻找船長的箱子");
        act("回中層甲板");
        act("走下樓梯");
        act("走向漆黑的深潭");
        List<string> boss = pressEnterUntil(atActions);
        Assert.Contains(boss, s => s.Contains("深潭水蛇"));
        Assert.Equal(1, session.flags["serpentDefeated"]);
        Assert.True(session.inventory.has("darkCrystal"));
        Assert.True(session.inventory.has("corsairSaber"));

        act("回中層甲板");
        act("回破裂的船身");
        act("離開洞窟");
        act("回河港村");
        List<string> ending = pressEnterUntil(atActions);
        Assert.Contains(ending, s => s.Contains("第二章「河港村」 完"));
        Assert.Equal(1, session.flags["chapter2Done"]);
    }

    [Fact]
    public void chapterThreeCanBePlayedToTheEnd()
    {
        GameSession session = startAt("riverPort", "metRin", "bossDefeated", "chapter1Done", "portIntro", "metBal", "serpentDefeated", "chapter2Done");
        session.addMember("rin", 12);
        session.addMember("bal", 12);
        foreach (PartyMember member in session.party)
        {
            member.gainExp(200_000);
        }

        act("往東方沙漠");
        act("前往砂岩城");
        pressEnterUntil(atActions); // the gate guard
        Assert.DoesNotContain("往月影綠洲", screen);

        act("學者館");
        pressEnterUntil(atActions);
        Assert.Equal(1, session.flags["scholarTalk"]);
        act("神殿");
        List<string> join = pressEnterUntil(atActions);
        Assert.Contains(join, s => s.Contains("小雪加入了隊伍"));
        Assert.Equal(4, session.party.Count);
        // Max level alone is not enough for a bot that only ever attacks; give everyone the gear a player would have by now.
        string[][] gear = [["steelSword", "steelPlate", "sandTurban"], ["hornBow", "desertCloak", "sandTurban"],
            ["warHammer", "steelPlate", "sandTurban", "shellShield"], ["crystalStaff", "desertCloak", "sandTurban"]];
        for (int i = 0; i < 4; i++)
        {
            foreach (string itemId in gear[i])
            {
                session.party[i].equip(game.content.item(itemId));
            }

            session.party[i].restore();
        }

        act("往月影綠洲");
        act("前往熾熱遺跡");
        act("進入遺跡");
        Assert.DoesNotContain("穿過石門", screen);
        act("往西側通道");
        act("按下月之石台");
        act("回大迴廊");
        act("往東側通道");
        act("按下日之石台");
        act("打開角落的寶箱");
        act("回大迴廊");
        act("穿過石門");
        pressEnterUntil(atActions);
        session.restoreParty(); // a player would have gone back to rest after the random battles on the way
        act("走近石像");
        // Only ever attacking cannot beat a boss built around healing: switch the battle to 自動 (the last command).
        pressEnterUntil(() => app.currentScene is BattleScene battle && battle.focusedWidget is MenuList && !battle.hasModal);
        press(Key.Up, Key.Enter);
        List<string> boss = pressEnterUntil(atActions);
        Assert.Contains(boss, s => s.Contains("自動"));
        Assert.Contains(boss, s => s.Contains("遺跡守衛"));
        Assert.Equal(1, session.flags["guardianDefeated"]);
        Assert.True(session.inventory.has("sealStone"));
        Assert.True(session.inventory.has("sunBlade"));

        act("回大迴廊");
        act("回前庭");
        act("回月影綠洲");
        act("回砂岩城");
        List<string> ending = pressEnterUntil(atActions);
        Assert.Contains(ending, s => s.Contains("第三章「砂岩城」 完"));
        Assert.Equal(1, session.flags["chapter3Done"]);
    }

    [Fact]
    public void finalChapterCanBePlayedToTheEnding()
    {
        GameSession session = startAt("sandstoneCity", "metRin", "bossDefeated", "chapter1Done", "portIntro", "metBal", "serpentDefeated",
            "chapter2Done", "cityIntro", "scholarTalk", "guardianDefeated", "chapter3Done");
        session.addMember("rin", 16);
        session.addMember("bal", 16);
        session.addMember("xue", 16);
        session.inventory.add("sealStone");
        session.inventory.add("hiPotion", 9);
        string[][] gear = [["mithrilSword", "mithrilMail", "mithrilHelm", "angelRing"], ["galeBow", "mysticRobe", "mithrilHelm", "angelRing"],
            ["titanAxe", "mithrilMail", "mithrilHelm", "shellShield"], ["sageStaff", "mysticRobe", "mithrilHelm", "angelRing"]];
        for (int i = 0; i < 4; i++)
        {
            session.party[i].gainExp(500_000);
            foreach (string itemId in gear[i])
            {
                session.party[i].equip(game.content.item(itemId));
            }

            session.party[i].restore();
        }

        act("往北方荒原");
        act("前往魔王城");
        pressEnterUntil(atActions); // the barrier gives way to the seal stone
        Assert.Equal(1, session.flags["castleOpen"]);

        act("女神像");
        pressEnterUntil("在這裡休息？");
        press(Key.Enter);
        pressEnterUntil("要記錄冒險嗎？");
        press(Key.Down, Key.Enter); // 否
        Assert.Equal("castleGate", session.respawnLocationId);

        act("進入城內");
        act("往東塔");
        act("熄滅黑色火炬");
        act("打開蒙塵的長箱");
        act("回大廳");
        Assert.DoesNotContain("走向王座之間", screen);
        act("往地下墓室");
        act("熄滅黑色火炬");
        act("回大廳");
        act("走向王座之間");
        pressEnterUntil(atActions);
        session.restoreParty();
        act("走向王座");
        pressEnterUntil(() => app.currentScene is BattleScene battle && battle.focusedWidget is MenuList && !battle.hasModal);
        press(Key.Up, Key.Enter); // 自動
        List<string> ending = pressEnterUntil(atActions, maxPresses: 600);

        Assert.Contains(ending, s => s.Contains("魔王・真身"));
        Assert.Contains(ending, s => s.Contains("終章「魔王城」 完"));
        Assert.Equal(1, session.flags["demonKingDefeated"]);
        Assert.Equal(1, session.flags["gameCleared"]);
        Assert.Equal("dawnTown", session.locationId);
        Assert.False(session.inventory.has("sealStone"));
        Assert.True(session.inventory.has("holySword"));
    }

    [Fact]
    public void autoBattlePlaysPartyTurnsUntilEscIsPressed()
    {
        GameSession session = game.startNew();
        session.party[0].gainExp(5000);
        app.pushScene(new EmptyScene());
        BattleScene battle = new(game, ["forestLord"], canEscape: false);
        app.pushScene(battle);
        bool atCommands() => battle.focusedWidget is MenuList && !battle.hasModal;
        pressEnterUntil(atCommands);

        press(Key.Up, Key.Enter); // 自動
        Assert.Contains("自動", screen);
        press(Key.Escape);
        Assert.DoesNotContain("自動 Esc", screen);
        pressEnterUntil(() => atCommands() || app.currentScene is not BattleScene);

        Assert.Same(battle, app.currentScene);
        Assert.Contains("指令", screen);
        Assert.Equal(BattleOutcome.Ongoing, battle.outcome);
    }

    [Fact]
    public void partyPanelKeepsThreeDigitMpApartFromItsLabel()
    {
        GameSession session = game.startNew();
        PartyMember xue = session.addMember("xue", level: 25);
        session.addMember("rin", level: 25);
        session.addMember("bal", level: 25);
        Assert.True(xue.mp >= 100);
        app.pushScene(new EmptyScene());
        app.pushScene(new BattleScene(game, ["slime"]));
        app.step(0);

        Assert.Contains($"MP {xue.mp}", screen);
        Assert.DoesNotContain($"MP{xue.mp}", screen);
    }

    [Fact]
    public void statusPanelShowsDerivedStatsAndEquipment()
    {
        GameSession session = game.startNew();
        session.inventory.add("potion", 3);
        app.pushScene(new EmptyScene());
        app.pushScene(new StatusScene(session));
        app.step(0);

        Assert.Contains("Lv.3", screen);
        Assert.Contains("木劍", screen);
        Assert.Contains($"攻擊 {session.party[0].stats.attack,3}", screen);
        Assert.Contains("傷藥 x3", screen);
        Assert.Contains("火球術", screen);
    }

    [Fact]
    public void journalShowsTheCurrentGoalAndTheStorySoFar()
    {
        GameSession session = game.startNew();
        session.flags["introDone"] = 1;
        session.flags["metRin"] = 1;
        app.pushScene(new EmptyScene());
        app.pushScene(new JournalScene(session));
        app.step(0);

        Assert.Contains("冒險日誌", screen);
        Assert.Contains("前往森林深處", screen);
        Assert.Contains("長老拜託艾倫", screen);
        Assert.Contains("救出了琳", screen);
        Assert.DoesNotContain("森林之主。", screen);
    }

    [Fact]
    public void partyMenuOpensTheJournal()
    {
        startAt("dawnTown");
        pressEnterUntilActions("長老家");

        press(Key.Tab);
        Assert.Contains("目標", screen);
        press(Key.Down, Key.Down, Key.Down, Key.Down, Key.Enter); // 道具 技能 裝備 狀態 [日誌]

        Assert.IsType<JournalScene>(app.currentScene);
        Assert.Contains("低語森林", screen);
    }

    [Fact]
    public void partyBanterPlaysOnceThenFallsBackToSmallTalk()
    {
        GameSession session = startAt("woodsClearing", "sawTracks", "metRin");
        session.addMember("rin", 4);
        pressEnterUntil(atActions);
        Assert.Contains("隊伍閒聊 !", screen);

        act("隊伍閒聊");
        List<string> first = pressEnterUntil(atActions);

        Assert.Contains(first, s => s.Contains("剛才謝謝你"));
        Assert.Equal(1, session.flags["banter_rescue"]);
        Assert.DoesNotContain("隊伍閒聊 !", screen);

        act("隊伍閒聊");
        List<string> second = pressEnterUntil(atActions);
        Assert.DoesNotContain(second, s => s.Contains("剛才謝謝你"));
    }

    [Fact]
    public void travelerRumorsDependOnTheStory()
    {
        GameSession session = startAt("dawnTown");
        pressEnterUntilActions("長老家");

        act("廣場的旅人");
        List<string> screens = pressEnterUntil(atActions);

        Assert.Contains(screens, s => s.Contains("行商"));
        Assert.Equal(1, session.flags["talkedToTraveler"]);
        Assert.DoesNotContain(screens, s => s.Contains("黑霧")); // only after the boss
    }

    [Fact]
    public void statusesAreNarrated()
    {
        GameSession session = game.startNew();
        BattleEngine engine = new(session, [game.content.enemy("venomShroom")]);
        Combatant hero = engine.party[0];
        Combatant shroom = engine.enemies[0];

        string text = string.Join("\n", BattleNarrator.narrate(
        [
            new DamageEvent(shroom, 30, false, 18, Effectiveness.Weak),
            new StatusAppliedEvent(hero, StatusKind.Poison, 3),
            new TurnSkippedEvent(hero, StatusKind.Sleep),
            new PoisonDamageEvent(hero, 5, 40),
            new StatusRemovedEvent(hero, StatusKind.Sleep, StatusEndReason.WokeUp),
        ]).Select(p => p.markup));

        Assert.Contains("效果拔群", text);
        Assert.Contains("中毒了", text);
        Assert.Contains("呼呼大睡", text);
        Assert.Contains("受到毒的侵蝕", text);
        Assert.Contains("被打醒了", text);
    }
}
