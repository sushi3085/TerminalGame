namespace TerminalGame.Tui.Rendering;

public enum ColorMode
{
    /// <summary>No color codes at all (NO_COLOR, dumb terminals). Attributes are still emitted.</summary>
    None,
    Ansi16,
    Ansi256,
    TrueColor,
}

public static class ColorModeDetector
{
    /// <summary>
    /// Picks the richest color mode the environment advertises.
    /// <paramref name="getVariable"/> is injectable so the logic can be unit-tested.
    /// </summary>
    public static ColorMode detect(Func<string, string?> getVariable, bool isWindows)
    {
        if (!string.IsNullOrEmpty(getVariable("NO_COLOR")))
        {
            return ColorMode.None;
        }

        string colorTerm = getVariable("COLORTERM") ?? string.Empty;
        if (colorTerm.Contains("truecolor", StringComparison.OrdinalIgnoreCase)
            || colorTerm.Contains("24bit", StringComparison.OrdinalIgnoreCase))
        {
            return ColorMode.TrueColor;
        }

        string term = getVariable("TERM") ?? string.Empty;
        if (term == "dumb")
        {
            return ColorMode.None;
        }

        if (term.Contains("256color", StringComparison.OrdinalIgnoreCase))
        {
            return ColorMode.Ansi256;
        }

        // Windows Terminal and modern conhost both support truecolor but rarely set COLORTERM.
        if (isWindows)
        {
            return ColorMode.TrueColor;
        }

        return ColorMode.Ansi16;
    }

    public static ColorMode detect() =>
        detect(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows());
}

/// <summary>RGB to palette index conversion for terminals without truecolor.</summary>
public static class ColorQuantizer
{
    /// <summary>The 16 standard xterm colors (0-7 normal, 8-15 bright).</summary>
    public static readonly (byte r, byte g, byte b)[] ansi16Palette =
    {
        (0, 0, 0), (205, 0, 0), (0, 205, 0), (205, 205, 0), (0, 0, 238), (205, 0, 205), (0, 205, 205), (229, 229, 229),
        (127, 127, 127), (255, 0, 0), (0, 255, 0), (255, 255, 0), (92, 92, 255), (255, 0, 255), (0, 255, 255), (255, 255, 255),
    };

    private static readonly int[] cubeLevels = { 0, 95, 135, 175, 215, 255 };

    public static int toAnsi16(Color color)
    {
        int best = 0;
        int bestDistance = int.MaxValue;
        for (int i = 0; i < ansi16Palette.Length; i++)
        {
            int distance = squaredDistance(color.r, color.g, color.b, ansi16Palette[i]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        return best;
    }

    /// <summary>Nearest entry of the xterm 256-color palette (6x6x6 cube or 24-step gray ramp, indices 16-255).</summary>
    public static int toAnsi256(Color color)
    {
        int ri = cubeIndex(color.r);
        int gi = cubeIndex(color.g);
        int bi = cubeIndex(color.b);
        int cubeDistance = squaredDistance(color.r, color.g, color.b, (cubeLevels[ri], cubeLevels[gi], cubeLevels[bi]));

        int average = (color.r + color.g + color.b) / 3;
        int grayStep = Math.Clamp((int)Math.Round((average - 8) / 10.0), 0, 23);
        int grayLevel = 8 + 10 * grayStep;
        int grayDistance = squaredDistance(color.r, color.g, color.b, (grayLevel, grayLevel, grayLevel));

        return grayDistance < cubeDistance ? 232 + grayStep : 16 + 36 * ri + 6 * gi + bi;
    }

    /// <summary>Inverse mapping, used by the headless VT emulator.</summary>
    public static Color fromAnsi256(int index)
    {
        if (index < 16)
        {
            (byte r, byte g, byte b) = ansi16Palette[index];
            return Color.rgb(r, g, b);
        }

        if (index >= 232)
        {
            byte level = (byte)(8 + 10 * (index - 232));
            return Color.rgb(level, level, level);
        }

        int cube = index - 16;
        return Color.rgb(
            (byte)cubeLevels[cube / 36],
            (byte)cubeLevels[cube / 6 % 6],
            (byte)cubeLevels[cube % 6]);
    }

    private static int cubeIndex(int component)
    {
        if (component < 48)
        {
            return 0;
        }

        return component < 115 ? 1 : (component - 35) / 40;
    }

    private static int squaredDistance(int r, int g, int b, (int r, int g, int b) other)
    {
        int dr = r - other.r;
        int dg = g - other.g;
        int db = b - other.b;
        return dr * dr + dg * dg + db * db;
    }
}
