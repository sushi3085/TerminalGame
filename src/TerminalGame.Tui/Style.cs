namespace TerminalGame.Tui;

[Flags]
public enum TextAttributes : byte
{
    None = 0,
    Bold = 1,
    Dim = 2,
    Italic = 4,
    Underline = 8,
    Blink = 16,
    Reverse = 32,
    Strikethrough = 64,
}

/// <summary>Foreground, background and attribute flags of a piece of text.</summary>
public readonly record struct Style(Color foreground, Color background, TextAttributes attributes)
{
    /// <summary>Fully transparent, no attributes: "draw with whatever is already there".</summary>
    public static readonly Style plain = default;

    /// <summary>The terminal's own colors, no attributes. Used to clear the screen.</summary>
    public static readonly Style terminalDefault = new(Color.terminalDefault, Color.terminalDefault, TextAttributes.None);

    public static Style fg(Color color) => new(color, Color.transparent, TextAttributes.None);

    public static Style bg(Color color) => new(Color.transparent, color, TextAttributes.None);

    public static Style of(Color foreground, Color background, TextAttributes attributes = TextAttributes.None) =>
        new(foreground, background, attributes);

    public Style withForeground(Color color) => this with { foreground = color };

    public Style withBackground(Color color) => this with { background = color };

    public Style withAttributes(TextAttributes extra) => this with { attributes = attributes | extra };

    /// <summary>
    /// Layers <paramref name="top"/> over this style: non-transparent colors of <paramref name="top"/> win,
    /// attributes are unioned.
    /// </summary>
    public Style overlay(Style top) => new(
        top.foreground.isTransparent ? foreground : top.foreground,
        top.background.isTransparent ? background : top.background,
        attributes | top.attributes);
}
