namespace TerminalGame.Tui.Input;

public enum Key
{
    None,

    /// <summary>A printable character; see <see cref="KeyEvent.character"/>.</summary>
    Char,
    Enter,
    Escape,
    Backspace,
    Tab,
    Up,
    Down,
    Left,
    Right,
    Home,
    End,
    PageUp,
    PageDown,
    Insert,
    Delete,
    F1, F2, F3, F4, F5, F6, F7, F8, F9, F10, F11, F12,
}

[Flags]
public enum KeyModifiers
{
    None = 0,
    Shift = 1,
    Alt = 2,
    Control = 4,
}

/// <summary>
/// Semantic input. Widgets react to actions rather than raw keys so that bindings can be remapped
/// in one place (<see cref="KeyMap"/>).
/// </summary>
public enum GameAction
{
    None,
    Up,
    Down,
    Left,
    Right,
    Confirm,
    Cancel,
    Menu,
    PageUp,
    PageDown,
}

public readonly record struct KeyEvent(Key key, char character, KeyModifiers modifiers, GameAction action)
{
    public static KeyEvent ofKey(Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        new(key, '\0', modifiers, GameAction.None);

    public static KeyEvent ofChar(char character, KeyModifiers modifiers = KeyModifiers.None) =>
        new(Key.Char, character, modifiers, GameAction.None);

    public bool isCtrl(char letter) =>
        key == Key.Char && (modifiers & KeyModifiers.Control) != 0 && char.ToLowerInvariant(character) == char.ToLowerInvariant(letter);
}
