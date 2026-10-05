using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Rpg.World;
using TerminalGame.Tui;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>Change equipment. Highlighting a candidate previews the stats it would give; ← → switches member.</summary>
public sealed class EquipScene : Scene
{
    public static readonly (EquipSlot slot, string label)[] slotLabels =
    {
        (EquipSlot.Weapon, "武器"),
        (EquipSlot.Body, "身體"),
        (EquipSlot.Head, "頭部"),
        (EquipSlot.Accessory, "飾品"),
    };

    private readonly GameSession session;
    private readonly MenuList slots = new();
    private readonly Border frame;
    private readonly TextBlock statsText = new();
    private readonly Label hint = new();
    private int memberIndex;

    public EquipScene(Game game)
    {
        session = game.session;
        slots.confirmed += chooseCandidate;
        slots.cancelled += () => application!.popScene();
        slots.selectionChanged += _ => previewNothing();

        StackPanel body = new(Orientation.Horizontal);
        body.add(new Border(slots, "部位") { layoutWidth = Length.cells(34), padding = new Thickness(1, 0, 0, 0) });
        body.add(new Border(statsText, "能力值") { padding = new Thickness(2, 0, 1, 0) });

        frame = new Border(body) { layoutHeight = Length.cells(12) };

        StackPanel layout = new(Orientation.Vertical);
        layout.add(frame);
        layout.add(hint);
        root = Ui.centeredPanel(layout, 70);
        root.theme = Palettes.forName(session.currentLocation().palette);
    }

    public override bool isTransparent => true;

    private PartyMember member => session.party[memberIndex];

    public override void onEnter() => refresh();

    public override void onKey(KeyEvent keyEvent)
    {
        if (hasModal || session.party.Count < 2)
        {
            return;
        }

        int step = keyEvent.action switch
        {
            GameAction.Left => -1,
            GameAction.Right => 1,
            _ => 0,
        };
        if (step != 0)
        {
            memberIndex = (memberIndex + step + session.party.Count) % session.party.Count;
            refresh();
        }
    }

    private void refresh()
    {
        frame.setTitle($"裝備 — [gold]{member.name}[/] Lv.{member.level}");
        int keep = slots.selectedIndex;
        slots.setItems(slotLabels.Select(s => MenuItem.of($"[dim]{s.label}[/]  {member.equippedIn(s.slot)?.name ?? "[dim]—[/]"}")));
        slots.select(keep);
        hint.setText(session.party.Count > 1 ? "[dim]← → 切換角色   Esc 返回[/]" : "[dim]Esc 返回[/]");
        setFocus(slots);
        previewNothing();
    }

    private void previewNothing() => statsText.setText(string.Join("\n", EquipText.compare(member.stats, member.stats)));

    private void preview(EquipSlot slot, ItemDef? item) =>
        statsText.setText(string.Join("\n", EquipText.compare(member.stats, member.previewStats(slot, item))));

    private void chooseCandidate(int slotIndex)
    {
        EquipSlot slot = slotLabels[slotIndex].slot;
        List<ItemDef?> candidates = new() { null };
        candidates.AddRange(session.inventory.entries
            .Select(e => session.content.item(e.itemId))
            .Where(i => i.kind == ItemKind.Equipment && i.slot == slot));

        MenuList list = Ui.showPicker(this, slotLabels[slotIndex].label,
            candidates.Select(i => MenuItem.of(i is null ? "[dim](卸下)[/]" : $"{i.name}  {EquipText.change(member, slot, i)}")),
            i =>
            {
                ItemDef? item = candidates[i];
                if (item is null)
                {
                    session.unequipToInventory(member, slot);
                }
                else
                {
                    session.equipFromInventory(member, item);
                }

                refresh();
            },
            refresh,
            width: 40,
            horizontal: HorizontalAlignment.Left,
            vertical: VerticalAlignment.Bottom);
        list.selectionChanged += i => preview(slot, candidates[i]);
        list.select(candidates.Count > 1 ? 1 : 0);
        preview(slot, candidates[list.selectedIndex]);
    }
}
