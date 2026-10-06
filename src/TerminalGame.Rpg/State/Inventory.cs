namespace TerminalGame.Rpg.State;

/// <summary>Item id → count, remembering the order items were first obtained (menus list them that way).</summary>
public sealed class Inventory
{
    public const int maxStack = 99;

    private readonly Dictionary<string, int> counts = new();
    private readonly List<string> order = new();

    /// <summary>Non-empty stacks in acquisition order.</summary>
    public IEnumerable<(string itemId, int count)> entries => order.Select(id => (id, counts[id]));

    public int count(string itemId) => counts.GetValueOrDefault(itemId);

    public bool has(string itemId, int amount = 1) => count(itemId) >= amount;

    /// <summary>Adds up to the stack cap; returns how many actually fit.</summary>
    public int add(string itemId, int amount = 1)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int current = count(itemId);
        int added = Math.Min(amount, maxStack - current);
        if (added <= 0)
        {
            return 0;
        }

        if (current == 0)
        {
            order.Add(itemId);
        }

        counts[itemId] = current + added;
        return added;
    }

    /// <summary>Removes <paramref name="amount"/> items; false (and no change) if there are not enough.</summary>
    public bool remove(string itemId, int amount = 1)
    {
        int current = count(itemId);
        if (amount <= 0 || current < amount)
        {
            return false;
        }

        if (current == amount)
        {
            counts.Remove(itemId);
            order.Remove(itemId);
        }
        else
        {
            counts[itemId] = current - amount;
        }

        return true;
    }
}
