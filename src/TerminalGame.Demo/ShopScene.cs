using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;
using TerminalGame.Tui;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>Buy and sell, one item per confirm. The right panel describes the highlighted item and, for gear, what it would change for each member.</summary>
public sealed class ShopScene : Scene
{
    private enum Mode
    {
        Top,
        Buy,
        Sell,
    }

    private const string keeper = "[cyan]店主[/]";

    private readonly GameSession session;
    private readonly ShopDef shop;
    private readonly MenuList list = new();
    private readonly Border listFrame;
    private readonly TextBlock details = new();
    private readonly Label goldLine = new();
    private readonly DialogueBox messages = new();
    private List<ItemDef> shown = new();
    private Mode mode = Mode.Top;

    public ShopScene(Game game, ShopDef shop)
    {
        session = game.session;
        this.shop = shop;

        list.confirmed += onConfirm;
        list.cancelled += onCancel;
        list.selectionChanged += _ => describe();
        listFrame = new Border(list, "") { layoutWidth = Length.cells(34), padding = new Thickness(1, 0, 0, 0) };

        StackPanel detailColumn = new(Orientation.Vertical);
        detailColumn.add(goldLine);
        detailColumn.add(new Spacer { layoutHeight = Length.cells(1) });
        detailColumn.add(details);

        StackPanel top = new(Orientation.Horizontal) { layoutHeight = Length.fill() };
        top.add(listFrame);
        top.add(new Border(detailColumn, "說明") { padding = new Thickness(1, 0, 1, 0) });

        messages.layoutHeight = Length.cells(6);

        StackPanel layout = new(Orientation.Vertical);
        layout.add(new Border(new Label($"[gold b]{shop.name}[/]", HorizontalAlignment.Center)) { layoutHeight = Length.cells(3) });
        layout.add(top);
        layout.add(messages);
        root = layout;
    }

    public override void onEnter()
    {
        messages.show("歡迎光臨！慢慢看喔。", keeper);
        showTop();
    }

    private void showTop()
    {
        mode = Mode.Top;
        listFrame.setTitle("要做什麼？");
        shown = new List<ItemDef>();
        list.setItems(new[] { MenuItem.of("買東西"), MenuItem.of("賣東西"), MenuItem.of("離開") });
        list.select(0);
        setFocus(list);
        describe();
    }

    private void showBuy(int keep = 0)
    {
        mode = Mode.Buy;
        listFrame.setTitle("購買");
        shown = shop.items.Select(session.content.item).ToList();
        list.setItems(shown.Select(item => MenuItem.of(
            $"{item.name,-8}[gold]{item.price,5}[/] G",
            ShopRules.maxAffordable(session, item) > 0)));
        list.select(Math.Min(keep, shown.Count - 1));
        setFocus(list);
        describe();
    }

    private void showSell(int keep = 0)
    {
        mode = Mode.Sell;
        listFrame.setTitle("出售");
        List<(ItemDef item, int count)> owned = session.inventory.entries
            .Select(e => (item: session.content.item(e.itemId), e.count))
            .ToList();
        shown = owned.Select(e => e.item).ToList();
        list.setItems(owned.Select(e => MenuItem.of(
            $"{e.item.name} x{e.count}  [gold]{ShopRules.sellPrice(e.item)}[/] G",
            ShopRules.canSell(e.item))));
        list.select(Math.Min(keep, Math.Max(0, shown.Count - 1)));
        setFocus(list);
        describe();
        if (shown.Count == 0)
        {
            messages.show("你身上好像沒有東西可以賣呢。", keeper);
        }
    }

    private void onConfirm(int index)
    {
        switch (mode)
        {
            case Mode.Top when index == 0:
                showBuy();
                break;
            case Mode.Top when index == 1:
                showSell();
                break;
            case Mode.Top:
                application!.popScene();
                break;
            case Mode.Buy:
                ItemDef bought = shown[index];
                if (ShopRules.buy(session, bought))
                {
                    messages.show($"[green]{bought.name}[/]，謝謝惠顧！", keeper);
                }

                showBuy(index);
                break;
            case Mode.Sell:
                ItemDef sold = shown[index];
                if (ShopRules.sell(session, sold))
                {
                    messages.show($"[green]{sold.name}[/]，我收下了。這是 [gold]{ShopRules.sellPrice(sold)}[/] G。", keeper);
                }

                showSell(index);
                break;
        }
    }

    private void onCancel()
    {
        if (mode == Mode.Top)
        {
            application!.popScene();
        }
        else
        {
            showTop();
        }
    }

    private void describe()
    {
        goldLine.setText($"持有金幣 [gold]{session.gold}[/] G");
        if (shown.Count == 0 || list.selectedIndex >= shown.Count)
        {
            details.setText("");
            return;
        }

        ItemDef item = shown[list.selectedIndex];
        List<string> lines = new() { item.description, $"[dim]持有 {session.inventory.count(item.id)}[/]" };
        if (item.slot is EquipSlot slot)
        {
            lines.Add("");
            foreach (PartyMember member in session.party)
            {
                lines.Add($"{member.name}  {EquipText.change(member, slot, item)}");
            }
        }

        details.setText(string.Join("\n", lines));
    }
}

/// <summary>Formats stat differences for shops and the equipment screen.</summary>
public static class EquipText
{
    private static readonly (string label, Func<StatBlock, int> get)[] stats =
    {
        ("HP", s => s.maxHp),
        ("MP", s => s.maxMp),
        ("攻擊", s => s.attack),
        ("防禦", s => s.defense),
        ("魔力", s => s.magic),
        ("速度", s => s.speed),
    };

    /// <summary>"攻擊 +4  速度 −1" for putting <paramref name="item"/> in <paramref name="slot"/>; "已裝備" / "沒有變化".</summary>
    public static string change(PartyMember member, EquipSlot slot, ItemDef? item)
    {
        if (item is not null && member.equippedIn(slot)?.id == item.id)
        {
            return "[dim]已裝備[/]";
        }

        if (item is not null && !item.canBeEquippedBy(member.def))
        {
            return "[dim]無法裝備[/]";
        }

        StatBlock now = member.stats;
        StatBlock next = member.previewStats(slot, item);
        List<string> parts = new();
        foreach ((string label, Func<StatBlock, int> get) in stats)
        {
            int delta = get(next) - get(now);
            if (delta != 0)
            {
                parts.Add(delta > 0 ? $"{label} [green]+{delta}[/]" : $"{label} [red]{delta}[/]");
            }
        }

        return parts.Count > 0 ? string.Join("  ", parts) : "[dim]沒有變化[/]";
    }

    /// <summary>"攻擊 13 → [green]17[/]" lines for the equipment screen preview.</summary>
    public static IEnumerable<string> compare(StatBlock now, StatBlock next) =>
        stats.Select(s =>
        {
            int a = s.get(now);
            int b = s.get(next);
            string arrow = a == b ? $"{b,3}" : b > a ? $"[green]{b,3}[/]" : $"[red]{b,3}[/]";
            return $"{s.label,-2} {a,3} → {arrow}";
        });
}
