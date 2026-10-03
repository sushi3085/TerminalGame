using System.Text;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Tests;

public class UnicodeWidthTests
{
    [Theory]
    [InlineData("a", 1)]
    [InlineData(" ", 1)]
    [InlineData("你", 2)]
    [InlineData("あ", 2)]
    [InlineData("ア", 2)]
    [InlineData("한", 2)]
    [InlineData("Ａ", 2)] // full-width Latin
    [InlineData("，", 2)] // full-width comma
    [InlineData("─", 1)] // box drawing is narrow (ambiguous width treated as 1)
    [InlineData("😀", 2)]
    [InlineData("é", 1)] // precomposed
    [InlineData("é", 1)] // e + combining acute is one grapheme, one column
    [InlineData("​", 0)] // zero width space
    public void graphemeWidth(string grapheme, int expected)
    {
        Assert.Equal(expected, UnicodeWidth.ofGrapheme(grapheme));
    }

    [Fact]
    public void controlCharactersHaveNoWidth()
    {
        Assert.Equal(0, UnicodeWidth.ofRune(new Rune('\n')));
        Assert.Equal(0, UnicodeWidth.ofRune(new Rune('\u001b')));
    }

    [Fact]
    public void emojiPresentationAndFlagsAreTwoColumns()
    {
        Assert.Equal(2, UnicodeWidth.ofGrapheme("❤️"));
        Assert.Equal(2, UnicodeWidth.ofGrapheme("🇹🇼"));
        Assert.Equal(2, UnicodeWidth.ofGrapheme("👨‍👩‍👧")); // ZWJ family
    }

    [Fact]
    public void stringWidthSumsGraphemes()
    {
        Assert.Equal(9, UnicodeWidth.ofString("HP 勇者ab"));
        Assert.Equal(0, UnicodeWidth.ofString(""));
    }

    [Fact]
    public void graphemesKeepClustersTogether()
    {
        string[] clusters = UnicodeWidth.graphemes("aéあ").ToArray();
        Assert.Equal(new[] { "a", "é", "あ" }, clusters);
    }

    [Fact]
    public void everyWideRangeBoundaryIsConsistent()
    {
        // Spot-check edges of the big CJK blocks.
        Assert.Equal(2, UnicodeWidth.ofRune(new Rune(0x4E00)));
        Assert.Equal(2, UnicodeWidth.ofRune(new Rune(0x9FFF)));
        Assert.Equal(1, UnicodeWidth.ofRune(new Rune(0x303F)));
        Assert.Equal(2, UnicodeWidth.ofRune(new Rune(0xAC00)));
        Assert.Equal(2, UnicodeWidth.ofRune(new Rune(0xD7A3)));
        Assert.Equal(2, UnicodeWidth.ofRune(new Rune(0x20000)));
    }
}
