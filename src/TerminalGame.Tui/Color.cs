using System.Globalization;

namespace TerminalGame.Tui;

/// <summary>
/// How a <see cref="Color"/> should be interpreted.
/// <c>Transparent</c> only exists on the drawing API ("keep whatever is underneath");
/// it never survives into a <see cref="Cell"/> stored in a screen buffer.
/// </summary>
public enum ColorKind : byte
{
    Transparent = 0,
    Default = 1,
    Rgb = 2,
}

/// <summary>A 24-bit color, the terminal's default color, or "transparent".</summary>
public readonly record struct Color(ColorKind kind, byte r, byte g, byte b)
{
    public static readonly Color transparent = default;
    public static readonly Color terminalDefault = new(ColorKind.Default, 0, 0, 0);

    public static readonly Color black = rgb(0, 0, 0);
    public static readonly Color white = rgb(240, 240, 240);
    public static readonly Color gray = rgb(140, 140, 140);
    public static readonly Color red = rgb(230, 70, 70);
    public static readonly Color green = rgb(90, 200, 90);
    public static readonly Color yellow = rgb(240, 210, 80);
    public static readonly Color blue = rgb(80, 140, 240);
    public static readonly Color magenta = rgb(200, 100, 220);
    public static readonly Color cyan = rgb(80, 200, 210);
    public static readonly Color orange = rgb(240, 150, 50);
    public static readonly Color pink = rgb(250, 140, 180);
    public static readonly Color purple = rgb(150, 100, 220);
    public static readonly Color gold = rgb(255, 200, 60);

    private static readonly Dictionary<string, Color> namedColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = black,
        ["white"] = white,
        ["gray"] = gray,
        ["grey"] = gray,
        ["red"] = red,
        ["green"] = green,
        ["yellow"] = yellow,
        ["blue"] = blue,
        ["magenta"] = magenta,
        ["cyan"] = cyan,
        ["orange"] = orange,
        ["pink"] = pink,
        ["purple"] = purple,
        ["gold"] = gold,
        ["default"] = terminalDefault,
    };

    public bool isTransparent => kind == ColorKind.Transparent;
    public bool isDefault => kind == ColorKind.Default;
    public bool isRgb => kind == ColorKind.Rgb;

    public static Color rgb(byte r, byte g, byte b) => new(ColorKind.Rgb, r, g, b);

    /// <summary>Linear interpolation in sRGB space; both colors must be RGB.</summary>
    public Color lerp(Color other, double t)
    {
        t = Math.Clamp(t, 0.0, 1.0);
        return rgb(
            (byte)Math.Round(r + (other.r - r) * t),
            (byte)Math.Round(g + (other.g - g) * t),
            (byte)Math.Round(b + (other.b - b) * t));
    }

    /// <summary>Parses "#rrggbb", "#rgb" or a known color name.</summary>
    public static bool tryParse(string text, out Color color)
    {
        color = transparent;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        text = text.Trim();
        if (namedColors.TryGetValue(text, out color))
        {
            return true;
        }

        if (text[0] != '#')
        {
            return false;
        }

        string hex = text.Substring(1);
        if (hex.Length == 3)
        {
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        }

        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
        {
            return false;
        }

        color = rgb((byte)(value >> 16), (byte)(value >> 8), (byte)value);
        return true;
    }

    public static Color fromHex(string text) =>
        tryParse(text, out Color color) ? color : throw new FormatException($"Invalid color '{text}'.");
}
