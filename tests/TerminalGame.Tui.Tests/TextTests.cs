using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Tests;

public class MarkupTests
{
    [Fact]
    public void plainTextIsUnchanged()
    {
        StyledText text = Markup.parse("Hello, 世界");
        Assert.Equal("Hello, 世界", text.toPlainString());
        Assert.Equal(11, text.glyphs.Sum(g => g.width));
    }

    [Fact]
    public void colorTagsStyleOnlyTheirSpan()
    {
        StyledText text = Markup.parse("a[red]b[/]c");
        Assert.Equal("abc", text.toPlainString());
        Assert.True(text.glyphs[0].style.foreground.isTransparent);
        Assert.Equal(Color.red, text.glyphs[1].style.foreground);
        Assert.True(text.glyphs[2].style.foreground.isTransparent);
    }

    [Fact]
    public void tagsNestAndCombine()
    {
        StyledText text = Markup.parse("[b][#ff0000]x[on:blue]y[/]z[/][/]w");
        Assert.Equal(TextAttributes.Bold, text.glyphs[0].style.attributes);
        Assert.Equal(Color.rgb(255, 0, 0), text.glyphs[0].style.foreground);
        Assert.Equal(Color.blue, text.glyphs[1].style.background);
        Assert.True(text.glyphs[2].style.background.isTransparent);
        Assert.Equal(TextAttributes.None, text.glyphs[3].style.attributes);

        StyledText combined = Markup.parse("[b gold]x");
        Assert.Equal(TextAttributes.Bold, combined.glyphs[0].style.attributes);
        Assert.Equal(Color.gold, combined.glyphs[0].style.foreground);
    }

    [Fact]
    public void unknownBracketsStayLiteral()
    {
        Assert.Equal("[Potion] x3", Markup.parse("[Potion] x3").toPlainString());
        Assert.Equal("[/] stray", Markup.parse("[/] stray").toPlainString());
        Assert.Equal("open [bracket", Markup.parse("open [bracket").toPlainString());
    }

    [Fact]
    public void doubleBracketEscapesLiteralBracket()
    {
        Assert.Equal("[red]", Markup.parse("[[red]").toPlainString());
        Assert.True(Markup.parse("[[red]").glyphs.All(g => g.style.foreground.isTransparent));
    }

    [Fact]
    public void baseStyleIsInheritedAndOverridden()
    {
        Style baseStyle = Style.fg(Color.green);
        StyledText text = Markup.parse("a[red]b[/]c", baseStyle);
        Assert.Equal(Color.green, text.glyphs[0].style.foreground);
        Assert.Equal(Color.red, text.glyphs[1].style.foreground);
        Assert.Equal(Color.green, text.glyphs[2].style.foreground);
    }

    [Fact]
    public void stripRemovesMarkup()
    {
        Assert.Equal("你獲得了傳說之劍", Markup.strip("你獲得了[gold b]傳說之劍[/]"));
    }

    [Fact]
    public void newlinesAndTabsAreNormalized()
    {
        StyledText text = Markup.parse("a\r\nb\tc");
        Assert.Equal("a\nb    c", text.toPlainString());
    }
}

public class TextLayoutTests
{
    private static string[] wrap(string text, int width) =>
        TextLayout.wrap(StyledText.plain(text), width)
            .Select(line => string.Concat(line.glyphs.Select(g => g.text)))
            .ToArray();

    [Fact]
    public void latinWrapsAtSpaces()
    {
        Assert.Equal(new[] { "the quick", "brown fox" }, wrap("the quick brown fox", 9));
        Assert.Equal(new[] { "the quick", "brown fox" }, wrap("the quick brown fox", 10));
    }

    [Fact]
    public void exactFitDoesNotWrap()
    {
        Assert.Equal(new[] { "hello" }, wrap("hello", 5));
        Assert.Equal(new[] { "hello", "world" }, wrap("hello world", 5));
    }

    [Fact]
    public void longWordsAreBrokenHard()
    {
        Assert.Equal(new[] { "abcde", "fghij", "kl" }, wrap("abcdefghijkl", 5));
    }

    [Fact]
    public void explicitNewlinesAreKeptIncludingBlankLines()
    {
        Assert.Equal(new[] { "a", "", "b" }, wrap("a\n\nb", 10));
        Assert.Equal(new[] { "a" }, wrap("a\n", 10));
        Assert.Empty(wrap("", 10));
    }

    [Fact]
    public void cjkBreaksBetweenCharactersByColumnWidth()
    {
        // Each ideograph is 2 columns, so 5 columns fit two characters.
        Assert.Equal(new[] { "你好", "世界" }, wrap("你好世界", 5));
        Assert.Equal(new[] { "你好世", "界" }, wrap("你好世界", 6));
    }

    [Fact]
    public void closingPunctuationNeverStartsALine()
    {
        // Width 4 would naturally put "。" first on line 2; kinsoku pulls the previous glyph down with it.
        string[] lines = wrap("你好。再見", 4);
        Assert.All(lines, line => Assert.False(line.StartsWith('。')));
        Assert.Equal(new[] { "你", "好。", "再見" }, lines);
    }

    [Fact]
    public void openingBracketNeverEndsALine()
    {
        string[] lines = wrap("你好「再見」", 4);
        Assert.All(lines, line => Assert.False(line.EndsWith('「')));
    }

    [Fact]
    public void mixedLatinAndCjkRespectsDisplayWidth()
    {
        string[] lines = wrap("HP 勇者 120", 7);
        Assert.All(lines, line => Assert.True(UnicodeWidth.ofString(line) <= 7, line));
    }

    [Fact]
    public void wrappedLinesNeverExceedWidth()
    {
        Random random = new(7);
        string alphabet = "ab 你好。，「 」xyz\n";
        for (int trial = 0; trial < 300; trial++)
        {
            string text = new(Enumerable.Range(0, random.Next(0, 60)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            int width = random.Next(2, 12);
            foreach (StyledLine line in TextLayout.wrap(StyledText.plain(text), width))
            {
                Assert.True(line.width <= width, $"'{text}' wrapped at {width} produced width {line.width}");
            }
        }
    }

    [Fact]
    public void wrappingLosesNoVisibleCharacters()
    {
        Random random = new(11);
        string alphabet = "ab你好。xyz ";
        for (int trial = 0; trial < 200; trial++)
        {
            string text = new(Enumerable.Range(0, random.Next(1, 50)).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            string original = text.Replace(" ", string.Empty);
            string wrapped = string.Concat(TextLayout.wrap(StyledText.plain(text), random.Next(2, 10))
                .SelectMany(l => l.glyphs.Select(g => g.text))).Replace(" ", string.Empty);
            Assert.Equal(original, wrapped);
        }
    }

    [Fact]
    public void styleSurvivesWrapping()
    {
        StyledText text = Markup.parse("aaaa [red]bbbb[/] cccc");
        IReadOnlyList<StyledLine> lines = TextLayout.wrap(text, 5);
        Assert.Equal(Color.red, lines[1].glyphs[0].style.foreground);
    }

    [Fact]
    public void truncateAddsEllipsisWithinWidth()
    {
        StyledText text = StyledText.plain("勇者的冒險開始了");
        StyledGlyph[] cut = TextLayout.truncate(text.glyphs, 7);
        Assert.True(TextLayout.widthOf(cut) <= 7);
        Assert.Equal("…", cut[^1].text);
        Assert.Equal(text.count, TextLayout.truncate(text.glyphs, 100).Length);
    }
}
