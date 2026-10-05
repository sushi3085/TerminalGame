using TerminalGame.Rpg.Battle;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Sim;

// Balance report for chapter 1. Each checkpoint is the party the design expects at that point of the story;
// every encounter of the area is fought many times by AutoPolicy (a careful player: heals, uses area skills).
//
//   dotnet run --project tools/TerminalGame.Sim [-- --runs 2000]
//
// Targets (ROADMAP M2): random battles ≥ 98% wins with ~60% HP left; story/boss fights ~70–90% at the expected level.

int runs = args.SkipWhile(a => a != "--runs").Skip(1).Select(int.Parse).FirstOrDefault(1000);
ContentDb content = ContentDb.loadDirectory(Path.Combine(AppContext.BaseDirectory, "content"));

AutoPolicy careful = new() { spendMp = false };
AutoPolicy allOut = new() { spendMp = true };

const string sword = "woodenSword";
Checkpoint edge = new("入口 Lv3", [new("hero", 3, sword, "clothTunic")], [("potion", 3)]);
Checkpoint path = new("小徑 Lv4", [new("hero", 4, sword, "clothTunic")], [("potion", 3)]);
Checkpoint rescue = new("救援 Lv4+琳4", [new("hero", 4, sword, "clothTunic"), new("rin", 4, "huntersBow", "clothTunic")], [("potion", 3)]);
Checkpoint clearing = new("空地 Lv5+琳5", [new("hero", 5, "bronzeSword", "clothTunic"), new("rin", 5, "huntersBow", "clothTunic", "leatherCap")], [("potion", 4)]);
Checkpoint deep = new("深處 Lv6+琳6", [new("hero", 6, "bronzeSword", "leatherArmor"), new("rin", 6, "huntersBow", "clothTunic", "leatherCap")], [("potion", 5)]);
Checkpoint boss = new("Boss Lv7+琳7", [new("hero", 7, "bronzeSword", "leatherArmor"), new("rin", 7, "longBow", "clothTunic", "leatherCap")], [("potion", 5), ("ether", 1)]);
Checkpoint bossHigh = new("Boss Lv8+琳8", [new("hero", 8, "bronzeSword", "leatherArmor"), new("rin", 8, "longBow", "clothTunic", "leatherCap")], [("potion", 5), ("ether", 1)]);
Checkpoint bossLow = new("Boss Lv6+琳6", [new("hero", 6, "bronzeSword", "leatherArmor"), new("rin", 6, "longBow", "clothTunic", "leatherCap")], [("potion", 5), ("ether", 1)]);

Console.WriteLine($"Chapter 1 balance — {runs} runs per row\n");

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

area("woodsEdge", edge, path);
area("woodsPath", path, rescue);
story("救援琳（野狼×2）", rescue, ["wolf", "wolf"]);
area("woodsClearing", clearing, deep);
area("woodsDeep", deep, boss);
story("森林之主", boss, ["venomShroom", "forestLord", "venomShroom"]);
story("森林之主（多練一級）", bossHigh, ["venomShroom", "forestLord", "venomShroom"]);
story("森林之主（少一級）", bossLow, ["venomShroom", "forestLord", "venomShroom"]);
