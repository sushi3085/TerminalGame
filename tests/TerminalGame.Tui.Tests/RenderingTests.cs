using TerminalGame.Tui.Backends;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Tests;

public class ScreenBufferTests
{
    private static readonly Style plain = Style.terminalDefault;

    [Fact]
    public void putStoresGlyphAndContinuationForWideCharacters()
    {
        ScreenBuffer buffer = new(6, 1);
        Assert.True(buffer.put(1, 0, "你", 2, plain));
        Assert.Equal("你", buffer.get(1, 0).glyph);
        Assert.True(buffer.get(2, 0).isContinuation);
        Assert.Equal(" 你   ", buffer.rowToString(0));
    }

    [Fact]
    public void wideGlyphThatDoesNotFitIsRejected()
    {
        ScreenBuffer buffer = new(3, 1);
        Assert.False(buffer.put(2, 0, "你", 2, plain));
        Assert.False(buffer.put(-1, 0, "a", 1, plain));
        Assert.False(buffer.put(0, 1, "a", 1, plain));
        Assert.Equal("   ", buffer.rowToString(0));
    }

    [Fact]
    public void overwritingRightHalfBlanksTheLeftHalf()
    {
        ScreenBuffer buffer = new(4, 1);
        buffer.put(0, 0, "你", 2, plain);
        buffer.put(1, 0, "x", 1, plain);
        Assert.Equal(" x  ", buffer.rowToString(0));
        Assert.False(buffer.get(0, 0).isContinuation);
    }

    [Fact]
    public void overwritingLeftHalfBlanksTheRightHalf()
    {
        ScreenBuffer buffer = new(4, 1);
        buffer.put(0, 0, "你", 2, plain);
        buffer.put(0, 0, "x", 1, plain);
        Assert.Equal("x   ", buffer.rowToString(0));
        Assert.False(buffer.get(1, 0).isContinuation);
    }

    [Fact]
    public void wideOverWideLeavesNoOrphans()
    {
        ScreenBuffer buffer = new(6, 1);
        buffer.put(0, 0, "你", 2, plain);
        buffer.put(2, 0, "好", 2, plain);
        buffer.put(1, 0, "世", 2, plain); // straddles both
        Assert.Equal(" 世  ", buffer.rowToString(0).Substring(0, 4));
        for (int x = 0; x < 6; x++)
        {
            if (buffer.get(x, 0).isContinuation)
            {
                Assert.True(x > 0 && buffer.get(x - 1, 0).glyph.Length > 0 && UnicodeWidth.ofGrapheme(buffer.get(x - 1, 0).glyph) == 2);
            }
        }
    }

    [Fact]
    public void transparentBackgroundKeepsWhatIsUnderneath()
    {
        ScreenBuffer buffer = new(2, 1);
        buffer.put(0, 0, " ", 1, Style.bg(Color.blue));
        buffer.put(0, 0, "a", 1, Style.fg(Color.red));
        Assert.Equal(Color.blue, buffer.get(0, 0).style.background);
        Assert.Equal(Color.red, buffer.get(0, 0).style.foreground);
        Assert.True(buffer.get(1, 0).style.background.isDefault);
    }
}

public class CanvasTests
{
    [Fact]
    public void subCanvasTranslatesAndClips()
    {
        ScreenBuffer buffer = new(10, 3);
        Canvas root = new(buffer);
        Canvas sub = root.sub(new Rect(2, 1, 4, 1));
        sub.drawText(0, 0, "abcdefgh");
        sub.drawText(-1, 0, "Z"); // outside the sub-canvas on the left
        Assert.Equal("          ", buffer.rowToString(0));
        Assert.Equal("  abcd    ", buffer.rowToString(1));
    }

    [Fact]
    public void nestedClipsIntersect()
    {
        ScreenBuffer buffer = new(10, 1);
        Canvas inner = new Canvas(buffer).sub(new Rect(0, 0, 5, 1)).sub(new Rect(3, 0, 5, 1));
        inner.fill(new Rect(0, 0, 5, 1), Style.plain, "#");
        Assert.Equal("   ##     ", buffer.rowToString(0));
    }

    [Fact]
    public void partiallyClippedWideGlyphBecomesBlank()
    {
        ScreenBuffer buffer = new(6, 1);
        Canvas sub = new Canvas(buffer).sub(new Rect(1, 0, 3, 1));
        sub.drawText(2, 0, "你"); // left half at col 3 (visible), right half at col 4 (clipped)
        Assert.DoesNotContain("你", buffer.rowToString(0));
        sub.drawText(-1, 0, "你"); // left half clipped, right half visible at col 1
        Assert.DoesNotContain("你", buffer.rowToString(0));
        sub.drawText(0, 0, "你");
        Assert.Contains("你", buffer.rowToString(0));
    }

    [Fact]
    public void drawTextReturnsAdvancedColumns()
    {
        Canvas canvas = new(new ScreenBuffer(20, 1));
        Assert.Equal(7, canvas.drawText(0, 0, "ab勇者c"));
    }

    [Fact]
    public void borderDrawsFourCornersAndEdges()
    {
        ScreenBuffer buffer = new(5, 3);
        new Canvas(buffer).drawBorder(new Rect(0, 0, 5, 3), BorderStyle.single, Style.plain);
        Assert.Equal(new[] { "┌───┐", "│   │", "└───┘" }, TestHelpers.rows(buffer));
    }
}

public class ColorTests
{
    [Theory]
    [InlineData(0, 0, 0, 16)]
    [InlineData(255, 255, 255, 231)]
    [InlineData(255, 0, 0, 196)]
    [InlineData(128, 128, 128, 244)] // gray ramp is closer than the cube
    public void ansi256Quantization(byte r, byte g, byte b, int expected)
    {
        Assert.Equal(expected, ColorQuantizer.toAnsi256(Color.rgb(r, g, b)));
    }

    [Fact]
    public void ansi16PicksNearestPaletteColor()
    {
        Assert.Equal(1, ColorQuantizer.toAnsi16(Color.rgb(200, 10, 10)));
        Assert.Equal(15, ColorQuantizer.toAnsi16(Color.rgb(250, 250, 250)));
        Assert.Equal(0, ColorQuantizer.toAnsi16(Color.rgb(5, 5, 5)));
    }

    [Fact]
    public void ansi256RoundTripStaysClose()
    {
        for (int index = 16; index < 256; index++)
        {
            Color color = ColorQuantizer.fromAnsi256(index);
            Color back = ColorQuantizer.fromAnsi256(ColorQuantizer.toAnsi256(color));
            Assert.Equal(color, back);
        }
    }

    [Theory]
    [InlineData("#ff8000", 255, 128, 0)]
    [InlineData("#f80", 255, 136, 0)]
    [InlineData("RED", 230, 70, 70)]
    public void parsesColors(string text, byte r, byte g, byte b)
    {
        Assert.True(Color.tryParse(text, out Color color));
        Assert.Equal(Color.rgb(r, g, b), color);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#gggggg")]
    [InlineData("nope")]
    public void rejectsBadColors(string text)
    {
        Assert.False(Color.tryParse(text, out _));
    }

    [Fact]
    public void lerpInterpolatesChannels()
    {
        Color mid = Color.rgb(0, 100, 200).lerp(Color.rgb(100, 200, 0), 0.5);
        Assert.Equal(Color.rgb(50, 150, 100), mid);
    }

    [Fact]
    public void colorModeDetectionFollowsEnvironment()
    {
        static Func<string, string?> env(params (string, string)[] pairs) =>
            name => pairs.Where(p => p.Item1 == name).Select(p => p.Item2).FirstOrDefault();

        Assert.Equal(ColorMode.None, ColorModeDetector.detect(env(("NO_COLOR", "1"), ("COLORTERM", "truecolor")), false));
        Assert.Equal(ColorMode.TrueColor, ColorModeDetector.detect(env(("COLORTERM", "truecolor")), false));
        Assert.Equal(ColorMode.TrueColor, ColorModeDetector.detect(env(("COLORTERM", "24bit")), false));
        Assert.Equal(ColorMode.Ansi256, ColorModeDetector.detect(env(("TERM", "xterm-256color")), false));
        Assert.Equal(ColorMode.None, ColorModeDetector.detect(env(("TERM", "dumb")), false));
        Assert.Equal(ColorMode.Ansi16, ColorModeDetector.detect(env(("TERM", "xterm")), false));
        Assert.Equal(ColorMode.TrueColor, ColorModeDetector.detect(env(), true));
    }
}

public class FrameRendererTests
{
    private static readonly Style plain = Style.terminalDefault;

    private static void assertScreensMatch(ScreenBuffer expected, ScreenBuffer actual, string context)
    {
        Assert.Equal(expected.width, actual.width);
        Assert.Equal(expected.height, actual.height);
        for (int y = 0; y < expected.height; y++)
        {
            for (int x = 0; x < expected.width; x++)
            {
                Cell want = expected.get(x, y);
                Cell got = actual.get(x, y);
                Assert.True(want == got, $"{context}: cell ({x},{y}) expected {describe(want)} but terminal shows {describe(got)}");
            }
        }
    }

    private static string describe(Cell cell) =>
        $"[{(cell.isContinuation ? "cont" : cell.glyph)} fg={cell.style.foreground} bg={cell.style.background} attr={cell.style.attributes}]";

    [Fact]
    public void firstFrameClearsAndPaintsEverythingNonBlank()
    {
        ScreenBuffer frame = new(10, 2);
        new Canvas(frame).drawText(0, 0, "Hello");
        FrameRenderer renderer = new(ColorMode.TrueColor);
        VtScreen terminal = new(10, 2);

        string output = renderer.render(frame);
        terminal.write(output);

        Assert.Contains("\u001b[2J", output);
        assertScreensMatch(frame, terminal.buffer, "first frame");
    }

    [Fact]
    public void identicalFrameProducesNoOutput()
    {
        ScreenBuffer frame = new(10, 2);
        new Canvas(frame).drawText(0, 0, "Hello");
        FrameRenderer renderer = new(ColorMode.TrueColor);
        renderer.render(frame);
        Assert.Equal(string.Empty, renderer.render(frame));
    }

    [Fact]
    public void onlyChangedCellsAreEmitted()
    {
        ScreenBuffer frame = new(20, 5);
        Canvas canvas = new(frame);
        canvas.drawText(0, 0, "static line");
        FrameRenderer renderer = new(ColorMode.TrueColor);
        renderer.render(frame);

        canvas.drawText(7, 0, "X");
        string output = renderer.render(frame);

        Assert.Contains("\u001b[1;8H", output);
        Assert.Contains("X", output);
        Assert.DoesNotContain("static", output);
        Assert.DoesNotContain("2J", output);
    }

    [Fact]
    public void adjacentChangesShareOneCursorMove()
    {
        ScreenBuffer frame = new(20, 1);
        FrameRenderer renderer = new(ColorMode.TrueColor);
        renderer.render(frame);
        new Canvas(frame).drawText(3, 0, "abc");
        string output = renderer.render(frame);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(output, @"\u001b\[\d+;\d+H"));
        Assert.Contains("abc", output);
    }

    [Fact]
    public void styleCodesAreSkippedWhenUnchanged()
    {
        ScreenBuffer frame = new(10, 1);
        new Canvas(frame).drawText(0, 0, "abcd", Style.fg(Color.red));
        FrameRenderer renderer = new(ColorMode.TrueColor);
        string output = renderer.render(frame);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(output, "38;2;230;70;70"));
    }

    [Fact]
    public void emitsTrueColor256And16ColorCodes()
    {
        ScreenBuffer frame = new(4, 1);
        new Canvas(frame).drawText(0, 0, "x", Style.of(Color.rgb(255, 0, 0), Color.rgb(0, 0, 255)));

        Assert.Contains("38;2;255;0;0", new FrameRenderer(ColorMode.TrueColor).render(frame));
        Assert.Contains("48;2;0;0;255", new FrameRenderer(ColorMode.TrueColor).render(frame));
        Assert.Contains("38;5;196", new FrameRenderer(ColorMode.Ansi256).render(frame));
        Assert.Contains("48;5;21", new FrameRenderer(ColorMode.Ansi256).render(frame));

        string sixteen = new FrameRenderer(ColorMode.Ansi16).render(frame);
        Assert.Contains("91", sixteen); // bright red foreground
        Assert.Contains("44", sixteen); // pure blue is nearer to standard blue (0,0,238) than bright blue

        string none = new FrameRenderer(ColorMode.None).render(frame);
        Assert.DoesNotContain("38;", none);
        Assert.DoesNotContain("48;", none);
    }

    [Fact]
    public void attributesAreEmittedAndResetBetweenCells()
    {
        ScreenBuffer frame = new(4, 1);
        Canvas canvas = new(frame);
        canvas.drawText(0, 0, "a", Style.plain.withAttributes(TextAttributes.Bold | TextAttributes.Underline));
        canvas.drawText(1, 0, "b");
        FrameRenderer renderer = new(ColorMode.TrueColor);
        VtScreen terminal = new(4, 1);
        terminal.write(renderer.render(frame));
        assertScreensMatch(frame, terminal.buffer, "attributes");
        Assert.Equal(TextAttributes.Bold | TextAttributes.Underline, terminal.buffer.get(0, 0).style.attributes);
        Assert.Equal(TextAttributes.None, terminal.buffer.get(1, 0).style.attributes);
    }

    [Fact]
    public void wideGlyphsAdvanceTwoColumnsAndSkipContinuation()
    {
        ScreenBuffer frame = new(8, 1);
        new Canvas(frame).drawText(0, 0, "你好a");
        FrameRenderer renderer = new(ColorMode.TrueColor);
        string output = renderer.render(frame);
        Assert.Contains("你好a", output); // one contiguous run: no cursor jump in between
        VtScreen terminal = new(8, 1);
        terminal.write(output);
        assertScreensMatch(frame, terminal.buffer, "wide");
    }

    [Fact]
    public void resizeForcesFullRepaint()
    {
        FrameRenderer renderer = new(ColorMode.TrueColor);
        ScreenBuffer small = new(5, 1);
        renderer.render(small);
        ScreenBuffer bigger = new(8, 2);
        new Canvas(bigger).drawText(0, 1, "hi");
        string output = renderer.render(bigger);
        Assert.Contains("\u001b[2J", output);
        VtScreen terminal = new(8, 2);
        terminal.write(output);
        assertScreensMatch(bigger, terminal.buffer, "resize");
    }

    [Fact]
    public void invalidateForcesFullRepaint()
    {
        ScreenBuffer frame = new(5, 1);
        new Canvas(frame).drawText(0, 0, "hi");
        FrameRenderer renderer = new(ColorMode.TrueColor);
        renderer.render(frame);
        renderer.invalidate();
        Assert.Contains("hi", renderer.render(frame));
    }

    /// <summary>
    /// The core guarantee: after every frame, a terminal that only ever saw our output shows exactly the frame.
    /// Random drawing mixes narrow, wide, combining and emoji glyphs, transparent/opaque colors, attributes,
    /// nested clips and negative offsets, on awkward sizes where wide glyphs straddle the right edge.
    /// </summary>
    [Theory]
    [InlineData(1, 7, 3)]
    [InlineData(2, 20, 6)]
    [InlineData(3, 1, 1)]
    [InlineData(4, 13, 5)]
    [InlineData(5, 40, 10)]
    public void randomizedDiffOutputReproducesEveryFrame(int seed, int width, int height)
    {
        Random random = new(seed);
        string[] glyphs = { "a", "b", "Z", " ", "あ", "你", "漢", "─", "é", "😀", "한", "▏", "1", "。" };
        Color?[] colors = { null, Color.red, Color.blue, Color.rgb(10, 200, 30), Color.terminalDefault, Color.black };
        TextAttributes[] attributes = { TextAttributes.None, TextAttributes.Bold, TextAttributes.Underline, TextAttributes.Reverse | TextAttributes.Italic };

        Style randomStyle() => Style.of(
            colors[random.Next(colors.Length)] ?? Color.transparent,
            colors[random.Next(colors.Length)] ?? Color.transparent,
            attributes[random.Next(attributes.Length)]);

        string randomText() => string.Concat(Enumerable.Range(0, random.Next(1, 8)).Select(_ => glyphs[random.Next(glyphs.Length)]));

        ScreenBuffer frame = new(width, height);
        FrameRenderer renderer = new(ColorMode.TrueColor);
        VtScreen terminal = new(width, height);

        for (int iteration = 0; iteration < 250; iteration++)
        {
            if (random.Next(25) == 0)
            {
                frame.clear(Style.terminalDefault);
            }

            Canvas canvas = new(frame);
            int operations = random.Next(1, 6);
            for (int op = 0; op < operations; op++)
            {
                Canvas target = random.Next(3) == 0
                    ? canvas.sub(new Rect(random.Next(-2, width), random.Next(-1, height), random.Next(0, width + 2), random.Next(0, height + 1)))
                    : canvas;
                switch (random.Next(4))
                {
                    case 0:
                    case 1:
                        target.drawText(random.Next(-3, width + 2), random.Next(-1, height + 1), randomText(), randomStyle());
                        break;
                    case 2:
                        target.fill(new Rect(random.Next(-2, width), random.Next(-1, height), random.Next(0, width), random.Next(0, height)), randomStyle(), glyphs[random.Next(3)]);
                        break;
                    default:
                        target.drawBorder(new Rect(random.Next(-1, width), random.Next(-1, height), random.Next(0, width + 1), random.Next(0, height + 1)), BorderStyle.rounded, randomStyle());
                        break;
                }
            }

            terminal.write(renderer.render(frame));
            assertScreensMatch(frame, terminal.buffer, $"seed {seed}, frame {iteration}");
        }
    }

    [Fact]
    public void outputWithoutAnyColorModeStillMatchesGlyphs()
    {
        ScreenBuffer frame = new(10, 2);
        new Canvas(frame).drawText(0, 0, "勇者 HP", Style.fg(Color.red));
        VtScreen terminal = new(10, 2);
        terminal.write(new FrameRenderer(ColorMode.None).render(frame));
        Assert.Equal("勇者 HP", terminal.toText().Split('\n')[0]);
    }
}
