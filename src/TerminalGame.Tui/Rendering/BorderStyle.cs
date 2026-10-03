namespace TerminalGame.Tui.Rendering;

/// <summary>The glyphs used to draw a box. Every glyph must be one column wide.</summary>
public sealed record BorderStyle(
    string topLeft,
    string top,
    string topRight,
    string left,
    string right,
    string bottomLeft,
    string bottom,
    string bottomRight)
{
    public static readonly BorderStyle single = new("┌", "─", "┐", "│", "│", "└", "─", "┘");
    public static readonly BorderStyle rounded = new("╭", "─", "╮", "│", "│", "╰", "─", "╯");
    public static readonly BorderStyle doubleLine = new("╔", "═", "╗", "║", "║", "╚", "═", "╝");
    public static readonly BorderStyle heavy = new("┏", "━", "┓", "┃", "┃", "┗", "━", "┛");

    /// <summary>Safe on terminals whose font or locale mis-measures box-drawing characters.</summary>
    public static readonly BorderStyle ascii = new("+", "-", "+", "|", "|", "+", "-", "+");
}
