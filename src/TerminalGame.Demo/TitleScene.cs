using TerminalGame.Rpg.State;
using TerminalGame.Tui;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

public sealed class TitleScene : Scene
{
    private readonly Game game;

    public TitleScene(Game game)
    {
        this.game = game;

        StackPanel logoText = new(Orientation.Vertical);
        logoText.add(new Label("[gold b]光 之 試 煉[/]", HorizontalAlignment.Center));
        logoText.add(new Label("[cyan]T E R M I N A L   Q U E S T[/]", HorizontalAlignment.Center));
        Border logo = new(logoText)
        {
            glyphs = BorderStyle.doubleLine,
            padding = new Thickness(4, 1, 4, 1),
            layoutWidth = Length.cells(40),
            layoutHeight = Length.cells(6),
        };

        SaveData? save = game.saves.peek(Game.saveSlot);
        MenuList menu = new(new[]
        {
            MenuItem.of("新的冒險"),
            MenuItem.of(save is null ? "繼續冒險" : $"繼續冒險 [dim]{save.summary(game.content)}[/]", save is not null),
            MenuItem.of("操作說明"),
            MenuItem.of("離開"),
        });
        menu.confirmed += onChoice;

        Label hint = new("[dim]↑↓ 選擇   Enter / Space / Z 確認   Esc / X 取消   Ctrl+C 離開[/]", HorizontalAlignment.Center);

        StackPanel column = new(Orientation.Vertical, spacing: 1);
        column.add(new Spacer());
        column.add(centered(logo));
        column.add(centered(new Border(menu) { layoutWidth = Length.cells(save is null ? 24 : 46), layoutHeight = Length.cells(6), glyphs = BorderStyle.rounded }));
        column.add(new Spacer());
        column.add(hint);

        root = column;
        setFocus(menu);
    }

    private static Widget centered(Widget child)
    {
        StackPanel row = new(Orientation.Horizontal) { layoutHeight = child.layoutHeight };
        row.add(new Spacer());
        row.add(child);
        row.add(new Spacer());
        return row;
    }

    private void onChoice(int index)
    {
        switch (index)
        {
            case 0:
                game.startNew();
                application!.replaceScene(new LocationScene(game));
                break;
            case 1:
                continueGame();
                break;
            case 2:
                showHelp();
                break;
            default:
                application!.quit();
                break;
        }
    }

    private void continueGame()
    {
        try
        {
            game.loadSaved();
        }
        catch (SaveException ex)
        {
            showMessage("讀取失敗", $"[red]存檔無法讀取：[/]{ex.Message}");
            return;
        }

        application!.replaceScene(new LocationScene(game, openingMessage: "[dim]（冒險繼續……）[/]"));
    }

    private void showMessage(string title, string markup)
    {
        TextBlock text = new(markup) { layoutWidth = Length.cells(44), layoutHeight = Length.cells(3) };
        Border box = new(text, title) { padding = new Thickness(2, 1, 2, 1) };
        MenuList ok = new(new[] { MenuItem.of("知道了") });
        StackPanel body = new(Orientation.Vertical);
        body.add(box);
        body.add(new Border(ok) { layoutHeight = Length.cells(3) });
        ok.confirmed += _ => closeModal(body);
        ok.cancelled += () => closeModal(body);
        showModal(body);
    }

    private void showHelp()
    {
        TextBlock text = new(
            "方向鍵移動游標，[gold]Enter[/] 確認，[gold]Esc[/] 取消。\n" +
            "對話中按確認可以[cyan]立刻顯示整段文字[/]，再按一次翻頁。\n" +
            "在地圖上按 [gold]Tab[/] 或 [gold]M[/] 打開隊伍選單。\n" +
            "在旅館休息時可以記錄冒險。")
        {
            layoutWidth = Length.cells(44),
            layoutHeight = Length.cells(6),
        };
        Border box = new(text, "說明") { padding = new Thickness(2, 1, 2, 1) };
        MenuList ok = new(new[] { MenuItem.of("知道了") });
        StackPanel body = new(Orientation.Vertical);
        body.add(box);
        body.add(new Border(ok) { layoutHeight = Length.cells(3) });
        ok.confirmed += _ => closeModal(body);
        ok.cancelled += () => closeModal(body);
        showModal(body);
    }
}
