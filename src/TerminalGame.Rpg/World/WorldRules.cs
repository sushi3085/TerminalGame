using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.Script;
using TerminalGame.Rpg.State;

namespace TerminalGame.Rpg.World;

/// <summary>What the party can do at a location, given the current story state.</summary>
public static class WorldRules
{
    public static LocationDef currentLocation(this GameSession session) =>
        session.content.location(session.locationId ?? throw new InvalidOperationException("the party is nowhere"));

    public static IReadOnlyList<ExitDef> visibleExits(this GameSession session, LocationDef location) =>
        location.exits.Where(e => Condition.check(e.@if, session)).ToList();

    public static IReadOnlyList<SpotDef> visibleSpots(this GameSession session, LocationDef location) =>
        location.spots.Where(s => Condition.check(s.@if, session)).ToList();

    public static string exitLabel(this GameSession session, ExitDef exit) =>
        exit.label.Length > 0 ? exit.label : "前往" + session.content.location(exit.to).name;

    /// <summary>The script to run on arrival, if any trigger matches.</summary>
    public static string? arrivalScript(this GameSession session, LocationDef location) =>
        location.onEnter.FirstOrDefault(t => Condition.check(t.@if, session))?.script;

    /// <summary>Moves the party along <paramref name="exit"/> and returns the new location.</summary>
    public static LocationDef travel(this GameSession session, ExitDef exit)
    {
        session.locationId = exit.to;
        return session.content.location(exit.to);
    }

    /// <summary>Rolls for a random battle; <paramref name="forced"/> skips the encounter-rate check (searching the area).</summary>
    public static EncounterDef? rollEncounter(this GameSession session, LocationDef location, bool forced = false)
    {
        if (!location.hasEncounters || (!forced && session.random.NextDouble() >= location.encounterRate))
        {
            return null;
        }

        int roll = session.random.Next(location.encounters.Sum(e => e.weight));
        foreach (EncounterDef encounter in location.encounters)
        {
            if (roll < encounter.weight)
            {
                return encounter;
            }

            roll -= encounter.weight;
        }

        return location.encounters[^1];
    }
}

/// <summary>Buying and selling. Items sell for half their price; key items cannot be sold.</summary>
public static class ShopRules
{
    public static int sellPrice(ItemDef item) => item.price / 2;

    public static bool canSell(ItemDef item) => item.kind != ItemKind.Key && item.price > 0;

    /// <summary>How many of <paramref name="item"/> the party can buy right now (gold and stack limit).</summary>
    public static int maxAffordable(GameSession session, ItemDef item) =>
        item.price <= 0 ? 0 : Math.Min(session.gold / item.price, Inventory.maxStack - session.inventory.count(item.id));

    public static bool buy(GameSession session, ItemDef item, int count = 1)
    {
        if (count <= 0 || count > maxAffordable(session, item))
        {
            return false;
        }

        session.gold -= item.price * count;
        session.inventory.add(item.id, count);
        return true;
    }

    public static bool sell(GameSession session, ItemDef item, int count = 1)
    {
        if (!canSell(item) || !session.inventory.remove(item.id, count))
        {
            return false;
        }

        session.gold += sellPrice(item) * count;
        return true;
    }
}
