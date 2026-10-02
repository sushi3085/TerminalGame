namespace TerminalGame.Tui.Input;

/// <summary>Maps physical keys to <see cref="GameAction"/>s. Rebind freely; the defaults suit a JRPG.</summary>
public sealed class KeyMap
{
    private readonly Dictionary<Key, GameAction> keyBindings = new();
    private readonly Dictionary<char, GameAction> charBindings = new();

    /// <summary>Arrows move; Enter / Space / Z confirm; Esc / Backspace / X cancel; Tab / M open the menu.</summary>
    public static KeyMap createDefault()
    {
        KeyMap map = new();
        map.bind(GameAction.Up, Key.Up);
        map.bind(GameAction.Down, Key.Down);
        map.bind(GameAction.Left, Key.Left);
        map.bind(GameAction.Right, Key.Right);
        map.bind(GameAction.Confirm, Key.Enter);
        map.bind(GameAction.Cancel, Key.Escape);
        map.bind(GameAction.Cancel, Key.Backspace);
        map.bind(GameAction.Menu, Key.Tab);
        map.bind(GameAction.PageUp, Key.PageUp);
        map.bind(GameAction.PageDown, Key.PageDown);
        map.bindChar(GameAction.Confirm, ' ');
        map.bindChar(GameAction.Confirm, 'z');
        map.bindChar(GameAction.Cancel, 'x');
        map.bindChar(GameAction.Menu, 'm');
        return map;
    }

    public KeyMap bind(GameAction action, params Key[] keys)
    {
        foreach (Key key in keys)
        {
            keyBindings[key] = action;
        }

        return this;
    }

    /// <summary>Binds printable characters (case-insensitive).</summary>
    public KeyMap bindChar(GameAction action, params char[] characters)
    {
        foreach (char character in characters)
        {
            charBindings[char.ToLowerInvariant(character)] = action;
        }

        return this;
    }

    public GameAction resolve(KeyEvent keyEvent)
    {
        if ((keyEvent.modifiers & (KeyModifiers.Control | KeyModifiers.Alt)) != 0)
        {
            return GameAction.None;
        }

        if (keyEvent.key == Key.Char)
        {
            return charBindings.TryGetValue(char.ToLowerInvariant(keyEvent.character), out GameAction byChar)
                ? byChar
                : GameAction.None;
        }

        return keyBindings.TryGetValue(keyEvent.key, out GameAction byKey) ? byKey : GameAction.None;
    }
}
