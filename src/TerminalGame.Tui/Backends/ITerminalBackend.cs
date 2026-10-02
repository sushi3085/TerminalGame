using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;

namespace TerminalGame.Tui.Backends;

/// <summary>
/// Everything the framework needs from a terminal. The real implementation is <see cref="ConsoleBackend"/>;
/// <see cref="HeadlessBackend"/> runs the same code against an in-memory screen for tests.
/// </summary>
public interface ITerminalBackend : IDisposable
{
    int width { get; }
    int height { get; }
    ColorMode colorMode { get; }

    /// <summary>Takes over the terminal (alternate screen, hidden cursor, …). Must be paired with <see cref="leave"/>.</summary>
    void enter();

    /// <summary>Restores the terminal to the state it had before <see cref="enter"/>. Safe to call repeatedly.</summary>
    void leave();

    void write(string text);

    /// <summary>Appends all key presses since the last call to <paramref name="output"/> without blocking.</summary>
    void pollKeys(List<KeyEvent> output);
}
