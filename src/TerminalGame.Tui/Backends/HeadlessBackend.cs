using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;

namespace TerminalGame.Tui.Backends;

/// <summary>
/// An in-memory terminal: output is replayed onto a <see cref="VtScreen"/>, input is scripted.
/// Use it to unit-test scenes and widgets, or to run a game without a TTY (CI, screenshots).
/// </summary>
public sealed class HeadlessBackend : ITerminalBackend
{
    private readonly Queue<KeyEvent> pendingKeys = new();

    public HeadlessBackend(int width = 80, int height = 24, ColorMode colorMode = ColorMode.TrueColor)
    {
        this.width = width;
        this.height = height;
        this.colorMode = colorMode;
        screen = new VtScreen(width, height);
    }

    public int width { get; private set; }
    public int height { get; private set; }
    public ColorMode colorMode { get; }

    public VtScreen screen { get; }

    public bool isEntered { get; private set; }

    /// <summary>Number of non-empty writes so far; an unchanged frame must not produce one.</summary>
    public int writeCount { get; private set; }

    public string lastWrite { get; private set; } = string.Empty;

    public string screenText => screen.toText();

    public void resize(int newWidth, int newHeight)
    {
        width = newWidth;
        height = newHeight;
        screen.resize(newWidth, newHeight);
    }

    public void queueKey(KeyEvent keyEvent) => pendingKeys.Enqueue(keyEvent);

    public void queueKeys(params Key[] keys)
    {
        foreach (Key key in keys)
        {
            pendingKeys.Enqueue(KeyEvent.ofKey(key));
        }
    }

    public void typeText(string text)
    {
        foreach (char character in text)
        {
            pendingKeys.Enqueue(KeyEvent.ofChar(character));
        }
    }

    public void enter() => isEntered = true;

    public void leave() => isEntered = false;

    public void write(string text)
    {
        if (text.Length == 0)
        {
            return;
        }

        writeCount++;
        lastWrite = text;
        screen.write(text);
    }

    public void pollKeys(List<KeyEvent> output)
    {
        while (pendingKeys.Count > 0)
        {
            output.Add(pendingKeys.Dequeue());
        }
    }

    public void Dispose() => leave();
}
