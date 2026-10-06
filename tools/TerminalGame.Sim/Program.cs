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

void area(string locationId, Checkpoint checkpoint, Checkpoint? next)
{
    LocationDef location = content.location(locationId);
    Console.WriteLine($"── {location.name}  @ {checkpoint.name}");
    Console.WriteLine($"   {"encounter",-30} {"win%",6} {"HP left",8} {"rounds",7} {"MP used",8}");
    int totalWeight = location.encounters.Sum(e => e.weight);
    double expPerBattle = 0;
    double roundsPerBattle = 0;
    foreach (EncounterDef encounter in location.encounters)
    {
        Summary s = Simulator.repeat(content, checkpoint, encounter.enemies, careful, runs);
        string names = string.Join("+", encounter.enemies.Select(id => content.enemy(id).name));
        Console.WriteLine($"   {names,-30} {s.winRate,6:P1} {s.hpLeft,8:P0} {s.rounds,7:F1} {s.mpUsed,8:F1}");
        double share = encounter.weight / (double)totalWeight;
        expPerBattle += share * encounter.enemies.Sum(id => content.enemy(id).exp);
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
        totalMinutes += minutes;
        Console.WriteLine($"   to reach Lv{to}: {needed} exp ≈ {battlesNeeded:F1} battles ≈ {minutes:F1} min");
    }

    Console.WriteLine();
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
    area("woodsPath", path, rescue);
    story("救援琳（野狼×2）", rescue, ["wolf", "wolf"]);
    area("woodsClearing", clearing, deep);
    area("woodsDeep", deep, ch1Boss(7));
    story("森林之主", ch1Boss(7), forestLord);
    story("森林之主（多練一級）", ch1Boss(8), forestLord);
    story("森林之主（少一級）", ch1Boss(6), forestLord);
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
    area("willowRoad", road, balRescue);
    story("巴爾登場（河蟹+海鷗+河蟹）", balRescue, ["riverCrab", "stormGull", "riverCrab"]);
    area("riverMouth", mouth, wreck);
    area("wreckEntrance", wreck, hold);
    area("wreckDeck", wreck, hold);
    area("wreckHold", hold, ch2Boss(12));
    story("深潭水蛇", ch2Boss(12), serpent);
    story("深潭水蛇（多練一級）", ch2Boss(13), serpent);
    story("深潭水蛇（少一級）", ch2Boss(11), serpent);
}

Console.WriteLine($"Estimated levelling time (random battles only): {totalMinutes:F0} min");
