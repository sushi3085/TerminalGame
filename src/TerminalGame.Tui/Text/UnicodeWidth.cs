using System.Globalization;
using System.Text;

namespace TerminalGame.Tui.Text;

/// <summary>
/// Terminal display width of text (0, 1 or 2 columns), in the spirit of POSIX <c>wcwidth</c>.
/// <para>
/// Limitations, by design: "East Asian Ambiguous" characters (box drawing, ●, …) are narrow.
/// Terminals configured with an ambiguous-is-wide locale will mis-align them; use
/// <see cref="Rendering.BorderStyle.ascii"/> there.
/// </para>
/// </summary>
public static class UnicodeWidth
{
    // East Asian Width = W or F, plus emoji presentation ranges. Sorted, non-overlapping.
    private static readonly (int lo, int hi)[] wideRanges =
    {
        (0x1100, 0x115F), (0x231A, 0x231B), (0x2329, 0x232A), (0x23E9, 0x23EC), (0x23F0, 0x23F0),
        (0x23F3, 0x23F3), (0x25FD, 0x25FE), (0x2614, 0x2615), (0x2648, 0x2653), (0x267F, 0x267F),
        (0x2693, 0x2693), (0x26A1, 0x26A1), (0x26AA, 0x26AB), (0x26BD, 0x26BE), (0x26C4, 0x26C5),
        (0x26CE, 0x26CE), (0x26D4, 0x26D4), (0x26EA, 0x26EA), (0x26F2, 0x26F3), (0x26F5, 0x26F5),
        (0x26FA, 0x26FA), (0x26FD, 0x26FD), (0x2705, 0x2705), (0x270A, 0x270B), (0x2728, 0x2728),
        (0x274C, 0x274C), (0x274E, 0x274E), (0x2753, 0x2755), (0x2757, 0x2757), (0x2795, 0x2797),
        (0x27B0, 0x27B0), (0x27BF, 0x27BF), (0x2B1B, 0x2B1C), (0x2B50, 0x2B50), (0x2B55, 0x2B55),
        (0x2E80, 0x303E), (0x3041, 0x33FF), (0x3400, 0x4DBF), (0x4E00, 0x9FFF), (0xA000, 0xA4CF),
        (0xA960, 0xA97F), (0xAC00, 0xD7A3), (0xF900, 0xFAFF), (0xFE10, 0xFE19), (0xFE30, 0xFE6F),
        (0xFF01, 0xFF60), (0xFFE0, 0xFFE6), (0x16FE0, 0x16FE4), (0x17000, 0x18AFF), (0x1B000, 0x1B2FF),
        (0x1F004, 0x1F004), (0x1F0CF, 0x1F0CF), (0x1F18E, 0x1F18E), (0x1F191, 0x1F19A), (0x1F200, 0x1F202),
        (0x1F210, 0x1F23B), (0x1F240, 0x1F248), (0x1F250, 0x1F251), (0x1F260, 0x1F265), (0x1F300, 0x1F320),
        (0x1F32D, 0x1F335), (0x1F337, 0x1F37C), (0x1F37E, 0x1F393), (0x1F3A0, 0x1F3CA), (0x1F3CF, 0x1F3D3),
        (0x1F3E0, 0x1F3F0), (0x1F3F4, 0x1F3F4), (0x1F3F8, 0x1F43E), (0x1F440, 0x1F440), (0x1F442, 0x1F4FC),
        (0x1F4FF, 0x1F53D), (0x1F54B, 0x1F54E), (0x1F550, 0x1F567), (0x1F57A, 0x1F57A), (0x1F595, 0x1F596),
        (0x1F5A4, 0x1F5A4), (0x1F5FB, 0x1F64F), (0x1F680, 0x1F6C5), (0x1F6CC, 0x1F6CC), (0x1F6D0, 0x1F6D2),
        (0x1F6D5, 0x1F6D7), (0x1F6DC, 0x1F6DF), (0x1F6EB, 0x1F6EC), (0x1F6F4, 0x1F6FC), (0x1F7E0, 0x1F7EB),
        (0x1F7F0, 0x1F7F0), (0x1F90C, 0x1F93A), (0x1F93C, 0x1F945), (0x1F947, 0x1F9FF), (0x1FA70, 0x1FAFF),
        (0x20000, 0x2FFFD), (0x30000, 0x3FFFD),
    };

    private const int emojiVariationSelector = 0xFE0F;

    /// <summary>Columns occupied by a single code point.</summary>
    public static int ofRune(Rune rune)
    {
        int value = rune.Value;
        if (value < 0x20 || (value >= 0x7F && value < 0xA0))
        {
            return 0;
        }

        if (value < 0x300)
        {
            return 1;
        }

        UnicodeCategory category = Rune.GetUnicodeCategory(rune);
        if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format)
        {
            return 0;
        }

        // Hangul Jamo medial vowels / final consonants combine with the preceding syllable.
        if (value >= 0x1160 && value <= 0x11FF)
        {
            return 0;
        }

        return isWide(value) ? 2 : 1;
    }

    /// <summary>Columns occupied by one extended grapheme cluster (what a user perceives as one character).</summary>
    public static int ofGrapheme(string grapheme)
    {
        int width = 0;
        int regionalIndicators = 0;
        bool emojiPresentation = false;
        foreach (Rune rune in grapheme.EnumerateRunes())
        {
            if (rune.Value == emojiVariationSelector)
            {
                emojiPresentation = true;
            }

            if (rune.Value >= 0x1F1E6 && rune.Value <= 0x1F1FF)
            {
                regionalIndicators++;
            }

            width = Math.Max(width, ofRune(rune));
        }

        // Emoji presentation selector (❤️), flag pairs (🇹🇼): rendered two columns wide by modern terminals.
        if ((emojiPresentation && width == 1) || regionalIndicators >= 2)
        {
            width = 2;
        }

        return width;
    }

    public static int ofString(string text)
    {
        int total = 0;
        foreach (string grapheme in graphemes(text))
        {
            total += ofGrapheme(grapheme);
        }

        return total;
    }

    public static IEnumerable<string> graphemes(string text)
    {
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            yield return enumerator.GetTextElement();
        }
    }

    private static bool isWide(int value)
    {
        int low = 0;
        int high = wideRanges.Length - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            (int lo, int hi) = wideRanges[mid];
            if (value < lo)
            {
                high = mid - 1;
            }
            else if (value > hi)
            {
                low = mid + 1;
            }
            else
            {
                return true;
            }
        }

        return false;
    }
}
