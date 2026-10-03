using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Tui.Tests;

public class StackPanelTests
{
    [Fact]
    public void verticalStackMixesAutoFixedAndFill()
    {
        StackPanel panel = new();
        Label top = new("top");
        Spacer middle = new();
        Label bottom = new("bottom") { layoutHeight = Length.cells(2) };
        panel.add(top);
        panel.add(middle);
        panel.add(bottom);

        panel.measure(new Size(20, 10));
        panel.arrange(new Rect(0, 0, 20, 10));

        Assert.Equal(new Rect(0, 0, 20, 1), top.bounds);
        Assert.Equal(new Rect(0, 1, 20, 7), middle.bounds);
        Assert.Equal(new Rect(0, 8, 20, 2), bottom.bounds);
    }

    [Fact]
    public void fillChildrenShareSpaceByWeightWithoutLosingCells()
    {
        StackPanel panel = new(Orientation.Horizontal);
        Spacer a = new(1);
        Spacer b = new(2);
        panel.add(a);
        panel.add(b);
        panel.arrange(new Rect(0, 0, 10, 1));

        Assert.Equal(10, a.bounds.width + b.bounds.width);
        Assert.Equal(0, a.bounds.x);
        Assert.Equal(a.bounds.width, b.bounds.x);
        Assert.True(b.bounds.width > a.bounds.width);
    }

    [Theory]
    [InlineData(7, 3)]
    [InlineData(10, 4)]
    [InlineData(1, 3)]
    [InlineData(0, 2)]
    public void fillDistributionAlwaysSumsToTheAvailableSpace(int total, int children)
    {
        StackPanel panel = new(Orientation.Horizontal, spacing: 0);
        for (int i = 0; i < children; i++)
        {
            panel.add(new Spacer(i + 1));
        }

        panel.arrange(new Rect(0, 0, total, 1));
        Assert.Equal(total, panel.children.Sum(c => c.bounds.width));
    }

    [Fact]
    public void spacingSeparatesChildren()
    {
        StackPanel panel = new(Orientation.Horizontal, spacing: 2);
        Label a = new("aa");
        Label b = new("bbb");
        panel.add(a);
        panel.add(b);
        Size desired = panel.measure(new Size(40, 5));
        panel.arrange(new Rect(0, 0, 40, 1));

        Assert.Equal(new Size(7, 1), desired);
        Assert.Equal(0, a.bounds.x);
        Assert.Equal(4, b.bounds.x);
    }

    [Fact]
    public void hiddenChildrenTakeNoSpace()
    {
        StackPanel panel = new();
        Label a = new("a");
        Label hidden = new("h") { visible = false };
        Label c = new("c");
        panel.add(a);
        panel.add(hidden);
        panel.add(c);
        panel.arrange(new Rect(0, 0, 5, 5));
        Assert.Equal(1, c.bounds.y);
    }

    [Fact]
    public void childrenAreClampedWhenContentDoesNotFit()
    {
        StackPanel panel = new();
        Label a = new("a") { layoutHeight = Length.cells(4) };
        Label b = new("b") { layoutHeight = Length.cells(4) };
        panel.add(a);
        panel.add(b);
        panel.arrange(new Rect(0, 0, 5, 6));
        Assert.Equal(4, a.bounds.height);
        Assert.Equal(2, b.bounds.height);
    }

    [Fact]
    public void aWidgetCanOnlyHaveOneParent()
    {
        Label label = new("x");
        new StackPanel().add(label);
        Assert.Throws<InvalidOperationException>(() => new StackPanel().add(label));
    }
}

public class BorderAndLabelTests
{
    [Fact]
    public void borderFramesChildWithTitle()
    {
        Border border = new(new Label("Hi"), "T");
        Assert.Equal(new[] { "┌ T ───┐", "│ Hi   │", "└──────┘" }, TestHelpers.renderRows(border, 8, 3));
    }

    [Fact]
    public void borderMeasuresChildPlusChrome()
    {
        Border border = new(new Label("Hello"));
        Assert.Equal(new Size(5 + 2 + 2, 1 + 2), border.measure(new Size(50, 50)));
    }

    [Fact]
    public void borderTitleIsTruncatedToFit()
    {
        Border border = new(null, "A very long title");
        string top = TestHelpers.renderRows(border, 10, 3)[0];
        Assert.Equal(10, UnicodeWidth.ofString(top));
        Assert.Contains("…", top);
    }

    [Fact]
    public void labelAlignsAndTruncates()
    {
        Assert.Equal("ab        ", TestHelpers.renderRows(new Label("ab"), 10, 1)[0]);
        Assert.Equal("        ab", TestHelpers.renderRows(new Label("ab", HorizontalAlignment.Right), 10, 1)[0]);
        Assert.Equal("    ab    ", TestHelpers.renderRows(new Label("ab", HorizontalAlignment.Center), 10, 1)[0]);
        Assert.Equal("勇者的…", TestHelpers.renderRows(new Label("勇者的冒險"), 7, 1)[0].TrimEnd());
    }

    [Fact]
    public void labelIgnoresTextAfterNewline()
    {
        Assert.Equal("one   ", TestHelpers.renderRows(new Label("one\ntwo"), 6, 1)[0]);
    }

    [Fact]
    public void textBlockWrapsAndScrolls()
    {
        TextBlock block = new("aaa bbb ccc ddd");
        Assert.Equal(new[] { "aaa bbb", "ccc ddd" }, TestHelpers.renderRows(block, 7, 2).Select(r => r.TrimEnd()).ToArray());

        // A one-row window over two wrapped lines can scroll by one.
        Assert.Equal(new[] { "aaa bbb" }, TestHelpers.renderRows(block, 7, 1).Select(r => r.TrimEnd()).ToArray());
        block.scrollOffset = 1;
        Assert.Equal(new[] { "ccc ddd" }, TestHelpers.renderRows(block, 7, 1).Select(r => r.TrimEnd()).ToArray());

        block.scrollOffset = 99; // clamped to the last position
        Assert.Equal(new[] { "ccc ddd" }, TestHelpers.renderRows(block, 7, 1).Select(r => r.TrimEnd()).ToArray());

        // When everything fits there is nothing to scroll.
        Assert.Equal(new[] { "aaa bbb", "ccc ddd" }, TestHelpers.renderRows(block, 7, 2).Select(r => r.TrimEnd()).ToArray());
    }

    [Fact]
    public void textBlockMeasuresWrappedHeight()
    {
        TextBlock block = new("aaa bbb ccc");
        Assert.Equal(new Size(7, 2), block.measure(new Size(7, 100)));
    }

    [Fact]
    public void artBlockCentersTheWholeBlockKeepingLinesAligned()
    {
        // The block is 4 wide and 2 tall; it is centered as a unit, lines stay left-aligned inside it.
        ArtBlock art = new("##\n####");
        string[] rows = TestHelpers.renderRows(art, 8, 4);
        Assert.Equal(new[] { "        ", "  ##    ", "  ####  ", "        " }, rows);
    }
}

public class ProgressBarTests
{
    [Fact]
    public void filledPortionUsesFillBackground()
    {
        ProgressBar bar = new(50, 100) { showNumbers = false, fillColor = Color.red };
        ScreenBuffer buffer = TestHelpers.renderToBuffer(bar, 10, 1);
        for (int x = 0; x < 5; x++)
        {
            Assert.Equal(Color.red, buffer.get(x, 0).style.background);
        }

        for (int x = 5; x < 10; x++)
        {
            Assert.Equal(Theme.defaultTheme.progressTrack, buffer.get(x, 0).style.background);
        }
    }

    [Fact]
    public void partialCellUsesEighthBlock()
    {
        // 3/16 of 4 cells = 0.75 cell = 6 eighths: no full cell, one partial glyph '▊' (6/8).
        ProgressBar bar = new(3, 16) { showNumbers = false, fillColor = Color.red };
        ScreenBuffer buffer = TestHelpers.renderToBuffer(bar, 4, 1);
        Assert.Equal("▊", buffer.get(0, 0).glyph);
        Assert.Equal(Color.red, buffer.get(0, 0).style.foreground);
        Assert.Equal(Theme.defaultTheme.progressTrack, buffer.get(0, 0).style.background);
    }

    [Fact]
    public void labelShowsCaptionAndNumbers()
    {
        ProgressBar bar = new(120, 200, "HP");
        string row = TestHelpers.renderRows(bar, 16, 1)[0];
        Assert.Contains("HP 120/200", row);
    }

    [Fact]
    public void ratioIsClamped()
    {
        Assert.Equal(1, new ProgressBar(500, 100).ratio);
        Assert.Equal(0, new ProgressBar(-5, 100).ratio);
        Assert.Equal(0, new ProgressBar(5, 0).ratio);
    }

    [Fact]
    public void gradientInterpolatesBetweenStops()
    {
        ProgressBar bar = new(50, 100) { showNumbers = false };
        bar.setGradient(Color.rgb(200, 0, 0), Color.rgb(0, 200, 0));
        ScreenBuffer buffer = TestHelpers.renderToBuffer(bar, 4, 1);
        Assert.Equal(Color.rgb(100, 100, 0), buffer.get(0, 0).style.background);
    }
}

public class MenuListTests
{
    private static MenuList createMenu(params bool[] enabled) =>
        new(enabled.Select((e, i) => MenuItem.of($"item{i}", e)));

    private static KeyEvent action(GameAction gameAction) => new(Key.None, '\0', KeyModifiers.None, gameAction);

    [Fact]
    public void downAndUpSkipDisabledItems()
    {
        MenuList menu = createMenu(true, false, true);
        Assert.Equal(0, menu.selectedIndex);
        menu.handleInput(action(GameAction.Down));
        Assert.Equal(2, menu.selectedIndex);
        menu.handleInput(action(GameAction.Up));
        Assert.Equal(0, menu.selectedIndex);
    }

    [Fact]
    public void wrapsAroundWhenEnabled()
    {
        MenuList menu = createMenu(true, true, true);
        menu.handleInput(action(GameAction.Up));
        Assert.Equal(2, menu.selectedIndex);
        menu.handleInput(action(GameAction.Down));
        Assert.Equal(0, menu.selectedIndex);

        menu.wrapAround = false;
        menu.handleInput(action(GameAction.Up));
        Assert.Equal(0, menu.selectedIndex);
    }

    [Fact]
    public void startsOnFirstEnabledItem()
    {
        Assert.Equal(1, createMenu(false, true).selectedIndex);
    }

    [Fact]
    public void allDisabledMenuDoesNotLoopForever()
    {
        MenuList menu = createMenu(false, false, false);
        menu.handleInput(action(GameAction.Down));
        menu.handleInput(action(GameAction.Up));
        Assert.True(true);
    }

    [Fact]
    public void confirmRaisesEventOnlyForEnabledItems()
    {
        MenuList menu = createMenu(true, true);
        List<int> confirmed = new();
        menu.confirmed += confirmed.Add;

        Assert.True(menu.handleInput(action(GameAction.Confirm)));
        menu.handleInput(action(GameAction.Down));
        menu.handleInput(action(GameAction.Confirm));
        Assert.Equal(new[] { 0, 1 }, confirmed);

        MenuList disabledSelected = new(new[] { MenuItem.of("x", enabled: false) });
        disabledSelected.confirmed += confirmed.Add;
        disabledSelected.handleInput(action(GameAction.Confirm));
        Assert.Equal(2, confirmed.Count);
    }

    [Fact]
    public void cancelAndConfirmBubbleWhenNobodyListens()
    {
        MenuList menu = createMenu(true);
        Assert.False(menu.handleInput(action(GameAction.Cancel)));
        Assert.False(menu.handleInput(action(GameAction.Confirm)));
        menu.cancelled += () => { };
        Assert.True(menu.handleInput(action(GameAction.Cancel)));
    }

    [Fact]
    public void selectionChangedFiresWithIndex()
    {
        MenuList menu = createMenu(true, true, true);
        List<int> seen = new();
        menu.selectionChanged += seen.Add;
        menu.handleInput(action(GameAction.Down));
        menu.select(0);
        menu.select(0);
        Assert.Equal(new[] { 1, 0 }, seen);
    }

    [Fact]
    public void rendersCursorOnSelectedRow()
    {
        MenuList menu = new(new[] { MenuItem.of("攻擊"), MenuItem.of("道具") });
        menu.handleInput(action(GameAction.Down));
        Assert.Equal(new[] { "  攻擊  ", "▶ 道具  " }, TestHelpers.renderRows(menu, 8, 2));
    }

    [Fact]
    public void scrollsToKeepSelectionVisible()
    {
        MenuList menu = new(Enumerable.Range(0, 10).Select(i => MenuItem.of($"row{i}")));
        TestHelpers.renderToBuffer(menu, 8, 3);
        for (int i = 0; i < 5; i++)
        {
            menu.handleInput(action(GameAction.Down));
        }

        string[] rows = TestHelpers.renderRows(menu, 8, 3);
        Assert.Contains(rows, r => r.Contains("row5") && r.StartsWith("▶"));
        Assert.DoesNotContain(rows, r => r.Contains("row0"));
    }

    [Fact]
    public void focusedSelectionUsesStrongerStyleThanUnfocused()
    {
        MenuList menu = new(new[] { MenuItem.of("a") });
        ScreenBuffer unfocused = TestHelpers.renderToBuffer(menu, 4, 1);
        menu.focused = true;
        ScreenBuffer focusedBuffer = TestHelpers.renderToBuffer(menu, 4, 1);
        Assert.NotEqual(unfocused.get(0, 0).style, focusedBuffer.get(0, 0).style);
    }

    [Fact]
    public void markupInLabelsIsRendered()
    {
        MenuList menu = new(new[] { MenuItem.of("[red]火球[/]術") });
        ScreenBuffer buffer = TestHelpers.renderToBuffer(menu, 10, 1);
        Assert.Equal(Color.red, buffer.get(2, 0).style.foreground);
        Assert.Equal("術", buffer.get(6, 0).glyph);
    }
}

public class DialogueBoxTests
{
    private static DialogueBox createBox(int width, int height, double charsPerSecond = 10)
    {
        DialogueBox box = new() { charsPerSecond = charsPerSecond, punctuationPause = 0 };
        box.measure(new Size(width, height));
        box.arrange(new Rect(0, 0, width, height));
        return box;
    }

    private static string[] render(DialogueBox box, int width, int height) =>
        TestHelpers.rows(renderOnly(box, width, height));

    private static ScreenBuffer renderOnly(DialogueBox box, int width, int height)
    {
        ScreenBuffer buffer = new(width, height);
        box.render(new Canvas(buffer));
        return buffer;
    }

    [Fact]
    public void typesOutGlyphByGlyph()
    {
        DialogueBox box = createBox(20, 5);
        box.show("Hello");
        Assert.True(box.isTyping);

        box.update(0.25); // 10 glyphs/second -> 2 glyphs after 0.25 s
        Assert.Contains("He", render(box, 20, 5)[1]);
        Assert.DoesNotContain("Hel", render(box, 20, 5)[1]);

        box.update(1.0);
        Assert.False(box.isTyping);
        Assert.Contains("Hello", render(box, 20, 5)[1]);
    }

    [Fact]
    public void punctuationAddsAPause()
    {
        DialogueBox box = createBox(20, 5, charsPerSecond: 10);
        box.punctuationPause = 1.0;
        box.show("a.bc");
        box.update(0.35); // 'a' (0.1) '.' (0.1) then a 1 s pause before 'b'
        Assert.Contains("a.", render(box, 20, 5)[1]);
        Assert.DoesNotContain("a.b", render(box, 20, 5)[1]);
        box.update(1.0);
        Assert.Contains("a.b", render(box, 20, 5)[1]);
    }

    [Fact]
    public void zeroSpeedShowsEverythingImmediately()
    {
        DialogueBox box = createBox(20, 5, charsPerSecond: 0);
        box.show("Instant");
        box.update(0.001);
        Assert.False(box.isTyping);
        Assert.Contains("Instant", render(box, 20, 5)[1]);
    }

    [Fact]
    public void confirmFinishesTypingThenPagesThenCompletes()
    {
        DialogueBox box = createBox(10, 4); // inner 6x2
        box.show("aaaaaa bbbbbb cccccc");
        int completedCount = 0;
        List<int> pageChanges = new();
        box.completed += () => completedCount++;
        box.pageChanged += pageChanges.Add;

        Assert.Equal(2, box.pageCount);
        Assert.True(box.advance()); // skips typing
        Assert.False(box.isTyping);
        Assert.Equal(0, box.currentPage);

        Assert.True(box.advance()); // next page
        Assert.Equal(1, box.currentPage);
        Assert.True(box.isTyping);
        Assert.Equal(new[] { 1 }, pageChanges);

        box.advance(); // finish typing on the last page
        Assert.Equal(0, completedCount);
        box.advance(); // dismiss
        Assert.Equal(1, completedCount);
        Assert.True(box.isFinished);
        Assert.False(box.advance());
    }

    [Fact]
    public void dialogueBoxSharesARowInsteadOfConsumingIt()
    {
        // Regression: an Auto-width dialogue box used to ask for the full row and squeeze its siblings to zero.
        StackPanel row = new(Orientation.Horizontal);
        Label left = new("L") { layoutWidth = Length.cells(10) };
        DialogueBox box = new();
        Label right = new("R") { layoutWidth = Length.cells(15) };
        row.add(left);
        row.add(box);
        row.add(right);

        row.measure(new Size(80, 6));
        row.arrange(new Rect(0, 0, 80, 6));

        Assert.Equal(10, left.bounds.width);
        Assert.Equal(55, box.bounds.width);
        Assert.Equal(15, right.bounds.width);
        Assert.Equal(65, right.bounds.x);
    }

    [Fact]
    public void confirmKeyDrivesAdvance()
    {
        DialogueBox box = createBox(20, 5);
        box.show("Hi");
        KeyEvent confirm = new(Key.Enter, '\0', KeyModifiers.None, GameAction.Confirm);
        Assert.True(box.handleInput(confirm));
        Assert.False(box.isTyping);
        Assert.False(box.handleInput(new KeyEvent(Key.Up, '\0', KeyModifiers.None, GameAction.Up)));
    }

    [Fact]
    public void cjkTextWrapsByColumns()
    {
        DialogueBox box = createBox(10, 4, charsPerSecond: 0); // inner width 6
        box.show("你好世界啊");
        box.update(0);
        string[] rows = render(box, 10, 4);
        Assert.Contains("你好世", rows[1]);
        Assert.Contains("界啊", rows[2]);
    }

    [Fact]
    public void speakerAppearsInFrameTitle()
    {
        DialogueBox box = createBox(20, 5);
        box.show("Hi", "[gold]勇者[/]");
        Assert.Contains("勇者", render(box, 20, 5)[0]);
    }

    [Fact]
    public void showingNewTextResetsState()
    {
        DialogueBox box = createBox(20, 5);
        box.show("first");
        box.advance();
        box.advance();
        Assert.True(box.isFinished);
        box.show("second");
        Assert.False(box.isFinished);
        Assert.True(box.isTyping);
        Assert.Equal(0, box.currentPage);
    }

    [Fact]
    public void showBeforeFirstLayoutPaginatesOnArrange()
    {
        DialogueBox box = new() { charsPerSecond = 0 };
        box.show("early text");
        box.measure(new Size(20, 5));
        box.arrange(new Rect(0, 0, 20, 5));
        box.update(0);
        Assert.Contains("early text", render(box, 20, 5)[1]);
    }

    [Fact]
    public void colorSpansSurviveTyping()
    {
        DialogueBox box = createBox(20, 5, charsPerSecond: 0);
        box.show("a[red]b[/]");
        box.update(0);
        ScreenBuffer buffer = renderOnly(box, 20, 5);
        Assert.Equal("b", buffer.get(3, 1).glyph);
        Assert.Equal(Color.red, buffer.get(3, 1).style.foreground);
    }
}
