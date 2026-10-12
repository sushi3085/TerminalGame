using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Sim;

// Balance report. Each checkpoint is the party the design expects at that point of the story; every encounter of
// an area is fought many times by AutoPolicy (a careful player: heals, uses area skills).
//
//   dotnet run -c Release --project tools/TerminalGame.Sim [-- --runs 2000] [-- --chapter 2]
//
// Targets: random battles ≥ 98% wins with ~60% HP left; story/boss fights ~70–90% at the expected level.

int runs = args.SkipWhile(a => a != "--runs").Skip(1).Select(int.Parse).FirstOrDefault(1000);
int onlyChapter = args.SkipWhile(a => a != "--chapter").Skip(1).Select(int.Parse).FirstOrDefault(0);
ContentDb content = ContentDb.loadDirectory(Path.Combine(AppContext.BaseDirectory, "content"));

AutoPolicy careful = new() { spendMp = false };
AutoPolicy allOut = new() { spendMp = true };
double totalMinutes = 0;
HashSet<string> countedTargets = new(); // areas sharing a levelling target (dungeon rooms) are alternatives, not additions

// Economy ledger: gold from the random battles needed to level between checkpoints plus boss gold, minus the gear each
// checkpoint expects (old gear sold back at half price; chest gear costs 0) and the consumables it carries beyond the
// previous one. Inns and restocking used-up items are not counted, so real wallets are somewhat thinner. A shortfall
// carries over as debt (the rest is bought later), so a later "covered" also pays off earlier gaps.
double wallet = content.newGame?.gold ?? 0;
List<string> economy = new();

void area(string locationId, Checkpoint checkpoint, Checkpoint? next)
{
    LocationDef location = content.location(locationId);
    Console.WriteLine($"── {location.name}  @ {checkpoint.name}");
    Console.WriteLine($"   {"encounter",-30} {"win%",6} {"HP left",8} {"rounds",7} {"MP used",8}");
    int totalWeight = location.encounters.Sum(e => e.weight);
    double expPerBattle = 0;
    double goldPerBattle = 0;
    double roundsPerBattle = 0;
    int leadLevel = checkpoint.party[0].level;
    foreach (EncounterDef encounter in location.encounters)
    {
        Summary s = Simulator.repeat(content, checkpoint, encounter.enemies, careful, runs);
        string names = string.Join("+", encounter.enemies.Select(id => content.enemy(id).name));
        Console.WriteLine($"   {names,-30} {s.winRate,6:P1} {s.hpLeft,8:P0} {s.rounds,7:F1} {s.mpUsed,8:F1}");
        double share = encounter.weight / (double)totalWeight;
        expPerBattle += share * encounter.enemies.Sum(id => Progression.scaledExp(content.enemy(id).exp, leadLevel, content.enemy(id).level));
        goldPerBattle += share * encounter.enemies.Sum(id => content.enemy(id).gold);
        roundsPerBattle += share * s.rounds;
    }

    (double battles, double defeatRate, double expPerTrip) = Simulator.trips(content, checkpoint, location, careful, runs / 4);
    Console.WriteLine($"   battles per trip before resting: {battles:F1}   trips ending in defeat: {defeatRate:P1}");

    if (next is not null)
    {
        // Experience the lead member needs to reach the next checkpoint's level, and roughly how long that takes
        // (about 6 s per round of reading plus 10 s of walking/menus per battle).
        int from = checkpoint.party[0].level;
        int to = next.party[0].level;
        int needed = Enumerable.Range(from, Math.Max(0, to - from)).Sum(Progression.expToNext);
        double battlesNeeded = needed / Math.Max(1, expPerBattle);
        double minutes = battlesNeeded * (roundsPerBattle * 6 + 10) / 60;
        if (countedTargets.Add(next.name))
        {
            totalMinutes += minutes;
            wallet += battlesNeeded * goldPerBattle;
        }

        Console.WriteLine($"   to reach Lv{to}: {needed} exp ≈ {battlesNeeded:F1} battles ≈ {minutes:F1} min, {goldPerBattle:F0} gold/battle");
    }

    Console.WriteLine();
}

// A boss's gold, collected once.
void loot(IReadOnlyList<string> enemyIds) => wallet += enemyIds.Sum(id => content.enemy(id).gold);

// Buys the gear `to` has and `from` lacks, selling what is no longer worn; records how much of it the wallet covers.
void shop(Checkpoint from, Checkpoint to)
{
    // Members who join between the two checkpoints bring their initial equipment.
    List<string> before = from.party.SelectMany(m => m.gear)
        .Concat(to.party.Where(m => from.party.All(f => f.characterId != m.characterId)).SelectMany(m => content.character(m.characterId).initialEquipment))
        .ToList();
    List<string> after = to.party.SelectMany(m => m.gear).ToList();
    List<string> bought = new(after);
    List<string> sold = new(before);
    foreach (string id in before)
    {
        if (bought.Remove(id))
        {
            sold.Remove(id);
        }
    }

    int gearCost = bought.Sum(id => content.item(id).price);
    int refund = sold.Sum(id => content.item(id).price / 2);
    int supplies = to.items.Sum(i => Math.Max(0, i.count - from.items.Where(f => f.itemId == i.itemId).Sum(f => f.count)) * content.item(i.itemId).price);
    if (gearCost == 0)
    {
        wallet -= supplies;
        return;
    }

    int cost = gearCost + supplies;
    double available = wallet + refund;
    economy.Add($"   {to.name,-16} gear {gearCost,6}  items {supplies,4}  sell-back {refund,5}  wallet {available,7:F0}  covered {Math.Clamp(available / cost, 0, 1),5:P0}");
    wallet = available - cost; // a shortfall carries over: the player buys the rest later
}

void story(string title, Checkpoint checkpoint, IReadOnlyList<string> enemyIds)
{
    Summary s = Simulator.repeat(content, checkpoint, enemyIds, allOut, runs);
    Console.WriteLine($"── {title} @ {checkpoint.name}");
    Console.WriteLine($"   win {s.winRate:P1}   HP left {s.hpLeft:P0}   rounds {s.rounds:F1}   MP used {s.mpUsed:F1}   items used {s.itemsUsed:F1}\n");
}

bool chapter(int number, string title)
{
    if (onlyChapter != 0 && onlyChapter != number)
    {
        return false;
    }

    Console.WriteLine($"════ Chapter {number}: {title} — {runs} runs per row ════\n");
    return true;
}

// ── Chapter 1 ────────────────────────────────────────────────────────────────────────────────
const string sword = "woodenSword";
Checkpoint edge = new("入口 Lv3", [new("hero", 3, sword, "clothTunic")], [("potion", 3)]);
Checkpoint path = new("小徑 Lv4", [new("hero", 4, sword, "clothTunic")], [("potion", 3), ("antidote", 2)]);
Checkpoint rescue = new("救援 Lv4+琳4", [new("hero", 4, sword, "clothTunic"), new("rin", 4, "huntersBow", "clothTunic")], [("potion", 3)]);
Checkpoint clearing = new("空地 Lv5+琳5", [new("hero", 5, "bronzeSword", "clothTunic"), new("rin", 5, "huntersBow", "clothTunic", "leatherCap")], [("potion", 4), ("antidote", 2)]);
Checkpoint deep = new("深處 Lv6+琳6", [new("hero", 6, "bronzeSword", "leatherArmor"), new("rin", 6, "huntersBow", "clothTunic", "leatherCap")], [("potion", 5), ("antidote", 2)]);
(string, int)[] bossItems = [("potion", 5), ("ether", 1), ("antidote", 2), ("mintLeaf", 2)];
Checkpoint ch1Boss(int level) => new($"Boss Lv{level}+琳{level}",
    [new("hero", level, "bronzeSword", "leatherArmor"), new("rin", level, "longBow", "clothTunic", "leatherCap")], bossItems);
string[] forestLord = ["venomShroom", "forestLord", "venomShroom"];

if (chapter(1, "低語森林"))
{
    area("woodsEdge", edge, path);
    shop(edge, path);
    area("woodsPath", path, rescue);
    story("救援琳（野狼×2）", rescue, ["wolf", "wolf"]);
    shop(rescue, clearing);
    area("woodsClearing", clearing, deep);
    shop(clearing, deep);
    area("woodsDeep", deep, ch1Boss(7));
    shop(deep, ch1Boss(7));
    story("森林之主", ch1Boss(7), forestLord);
    story("森林之主（多練一級）", ch1Boss(8), forestLord);
    story("森林之主（少一級）", ch1Boss(6), forestLord);
    loot(forestLord);
}

// ── Chapter 2 ────────────────────────────────────────────────────────────────────────────────
(string, int)[] ch2Items = [("potion", 5), ("hiPotion", 2), ("antidote", 2), ("mintLeaf", 1), ("phoenixDown", 1)];
Checkpoint road = new("大道 Lv8+琳8",
    [new("hero", 8, "bronzeSword", "leatherArmor", "acornCharm"), new("rin", 8, "longBow", "clothTunic", "leatherCap")], ch2Items);
MemberSetup[] tier2(int level) =>
[
    new("hero", level, "ironSword", "chainMail", "leatherCap", "acornCharm"),
    new("rin", level, "compositeBow", "rangerVest", "leatherCap"),
    new("bal", level, "boardingAxe", "clothTunic", "harborShield"),
];
Checkpoint balRescue = new("巴爾 Lv9×3",
    [new("hero", 9, "bronzeSword", "leatherArmor", "acornCharm"), new("rin", 9, "longBow", "clothTunic", "leatherCap"), new("bal", 9, "boardingAxe", "clothTunic", "harborShield")],
    ch2Items);
Checkpoint mouth = new("沙洲 Lv9×3", tier2(9), ch2Items);
Checkpoint wreck = new("洞窟 Lv10×3", tier2(10), ch2Items);
Checkpoint hold = new("底艙 Lv11×3",
    [new("hero", 11, "corsairSaber", "chainMail", "ironHelm", "acornCharm"), new("rin", 11, "compositeBow", "rangerVest", "ironHelm"),
     new("bal", 11, "ironAxe", "chainMail", "leatherCap", "harborShield")], ch2Items);
(string, int)[] serpentItems = [("potion", 5), ("hiPotion", 3), ("ether", 2), ("phoenixDown", 2), ("antidote", 2), ("mintLeaf", 2)];
Checkpoint ch2Boss(int level) => new($"水蛇 Lv{level}×3",
    [new("hero", level, "corsairSaber", "chainMail", "ironHelm", "pearlAmulet"), new("rin", level, "compositeBow", "rangerVest", "ironHelm"),
     new("bal", level, "ironAxe", "chainMail", "leatherCap", "shellShield")], serpentItems);
string[] serpent = ["bloodLeech", "deepSerpent", "bloodLeech"];

if (chapter(2, "河港村"))
{
    shop(ch1Boss(7), road);
    area("willowRoad", road, balRescue);
    shop(road, balRescue);
    story("巴爾登場（河蟹+海鷗+河蟹）", balRescue, ["riverCrab", "stormGull", "riverCrab"]);
    shop(balRescue, mouth);
    area("riverMouth", mouth, wreck);
    area("wreckEntrance", wreck, hold);
    area("wreckDeck", wreck, hold);
    shop(wreck, hold);
    area("wreckHold", hold, ch2Boss(12));
    shop(hold, ch2Boss(12));
    story("深潭水蛇", ch2Boss(12), serpent);
    story("深潭水蛇（多練一級）", ch2Boss(13), serpent);
    story("深潭水蛇（少一級）", ch2Boss(11), serpent);
    loot(serpent);
}

// ── Chapter 3 ────────────────────────────────────────────────────────────────────────────────
(string, int)[] ch3Items = [("potion", 4), ("hiPotion", 4), ("ether", 2), ("antidote", 2), ("mintLeaf", 2), ("phoenixDown", 2)];
Checkpoint desert = new("沙原 Lv12×3",
    [new("hero", 12, "corsairSaber", "chainMail", "ironHelm", "pearlAmulet"), new("rin", 12, "compositeBow", "rangerVest", "ironHelm"),
     new("bal", 12, "ironAxe", "chainMail", "leatherCap", "shellShield")], ch3Items);
MemberSetup[] tier3(int level, string heroWeapon = "steelSword", string balShield = "shellShield", string xueRing = "acornCharm") =>
[
    new("hero", level, heroWeapon, "steelPlate", "sandTurban", "pearlAmulet"),
    new("rin", level, "hornBow", "desertCloak", "sandTurban", "acornCharm"),
    new("bal", level, "warHammer", "steelPlate", "ironHelm", balShield),
    new("xue", level, "crystalStaff", "priestRobe", "sandTurban", xueRing),
];
Checkpoint oasisParty = new("綠洲 Lv13×4",
    [new("hero", 13, "corsairSaber", "chainMail", "ironHelm", "pearlAmulet"), new("rin", 13, "compositeBow", "rangerVest", "ironHelm"),
     new("bal", 13, "ironAxe", "chainMail", "leatherCap", "shellShield"), new("xue", 13, "oakStaff", "priestRobe")], ch3Items);
Checkpoint court = new("前庭 Lv13×4", tier3(13), ch3Items);
Checkpoint halls = new("迴廊 Lv14×4", tier3(14, balShield: "guardianShield"), ch3Items);
(string, int)[] guardianItems = [("potion", 4), ("hiPotion", 5), ("ether", 2), ("hiEther", 2), ("phoenixDown", 3), ("mintLeaf", 2)];
Checkpoint ch3Boss(int level) => new($"守衛 Lv{level}×4", tier3(level, "sunBlade", "guardianShield", "frostRing"), guardianItems);
string[] guardian = ["fireSpirit", "ruinGuardian", "fireSpirit"];
string[] captain = ["drownedSailor", "drownedCaptain", "drownedSailor"];
Checkpoint tier3At(int level) => new($"迴廊 Lv{level}×4", tier3(level, balShield: "guardianShield"), ch3Items);

if (chapter(3, "砂岩城"))
{
    shop(ch2Boss(12), desert);
    area("desertRoad", desert, oasisParty);
    shop(desert, oasisParty);
    area("oasis", oasisParty, court);
    shop(oasisParty, court);
    area("ruinsCourt", court, halls);
    shop(court, halls);
    area("ruinsHall", halls, ch3Boss(16));
    area("ruinsWest", halls, ch3Boss(16));
    area("ruinsEast", halls, ch3Boss(16));
    // Optional: the old sailor's errand sends the chapter 3 party back to the wreck's hold.
    story("支線：幽靈船長", halls, captain);
    story("支線：幽靈船長（多練一級）", tier3At(15), captain);
    shop(halls, ch3Boss(16));
    story("遺跡守衛", ch3Boss(16), guardian);
    story("遺跡守衛（多練一級）", ch3Boss(17), guardian);
    story("遺跡守衛（少一級）", ch3Boss(15), guardian);
    story("遺跡守衛（少兩級）", ch3Boss(14), guardian);
    loot(guardian);
}

// ── Final chapter ────────────────────────────────────────────────────────────────────────────
(string, int)[] ch4Items = [("hiPotion", 6), ("ether", 2), ("hiEther", 2), ("phoenixDown", 3), ("mintLeaf", 2)];
Checkpoint wastes = new("荒原 Lv16×4", tier3(16, "sunBlade", "guardianShield", "frostRing"), ch4Items);
Checkpoint castle = new("城內 Lv17×4",
[
    new("hero", 17, "holySword", "mithrilMail", "sandTurban", "pearlAmulet"),
    new("rin", 17, "hornBow", "desertCloak", "sandTurban", "acornCharm"),
    new("bal", 17, "warHammer", "steelPlate", "ironHelm", "dragonShield"),
    new("xue", 17, "crystalStaff", "priestRobe", "sandTurban", "frostRing"),
], ch4Items);
(string, int)[] finalItems = [("hiPotion", 6), ("hiEther", 3), ("phoenixDown", 4), ("elixir", 2), ("mintLeaf", 2)];
Checkpoint finalBoss(int level) => new($"魔王 Lv{level}×4",
[
    new("hero", level, "holySword", "mithrilMail", "mithrilHelm", "angelRing"),
    new("rin", level, "galeBow", "mysticRobe", "sandTurban", "acornCharm"),
    new("bal", level, "titanAxe", "mithrilMail", "ironHelm", "dragonShield"),
    new("xue", level, "sageStaff", "mysticRobe", "sandTurban", "frostRing"),
], finalItems);
string[] demonKing = ["shadowKnight", "demonKing", "shadowKnight"];

if (chapter(4, "魔王城"))
{
    shop(ch3Boss(16), wastes);
    area("northWastes", wastes, castle);
    shop(wastes, castle);
    area("castleHall", castle, finalBoss(19));
    area("castleTower", castle, finalBoss(19));
    area("castleCrypt", castle, finalBoss(19));
    shop(castle, finalBoss(19));
    story("魔王（兩階段）", finalBoss(19), demonKing);
    story("魔王（多練一級）", finalBoss(20), demonKing);
    story("魔王（少一級）", finalBoss(18), demonKing);
    story("魔王（少兩級）", finalBoss(17), demonKing);
}

// Each area's levelling segment counts once; story, bosses, shops and walking are not included (roughly +25 min overall).
Console.WriteLine($"Estimated levelling time (random battles only): {totalMinutes:F0} min");
if (onlyChapter == 0)
{
    Console.WriteLine("\nEconomy (gold from levelling battles + bosses vs. the gear each checkpoint expects):");
    economy.ForEach(Console.WriteLine);
}
