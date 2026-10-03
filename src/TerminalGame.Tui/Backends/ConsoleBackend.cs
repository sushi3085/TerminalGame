using System.Runtime.InteropServices;
using System.Text;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;

namespace TerminalGame.Tui.Backends;

/// <summary>
/// The real terminal. Output is written as UTF-8 bytes straight to the stdout stream (one write per frame),
/// input is polled with <see cref="Console.KeyAvailable"/> so the game loop never blocks and no extra thread exists.
/// </summary>
public sealed class ConsoleBackend : ITerminalBackend
{
    private const string esc = "\u001b[";

    // Alternate screen, hide cursor, disable auto-wrap (so painting the bottom-right cell cannot scroll), clear.
    private const string enterSequence = esc + "?1049h" + esc + "?25l" + esc + "?7l" + esc + "0m" + esc + "2J" + esc + "H";
    private const string leaveSequence = esc + "0m" + esc + "?7h" + esc + "?25h" + esc + "?1049l";

    private readonly UTF8Encoding utf8 = new(false);
    private readonly Stream stdout = Console.OpenStandardOutput();
    private bool entered;

    public ConsoleBackend(ColorMode? colorMode = null)
    {
        this.colorMode = colorMode ?? ColorModeDetector.detect();
    }

    public ColorMode colorMode { get; }

    public int width => queryWindow(() => Console.WindowWidth, 80);

    public int height => queryWindow(() => Console.WindowHeight, 24);

    public void enter()
    {
        if (entered)
        {
            return;
        }

        if (Console.IsOutputRedirected)
        {
            throw new InvalidOperationException("ConsoleBackend needs an interactive terminal; stdout is redirected.");
        }

        if (OperatingSystem.IsWindows())
        {
            enableWindowsVirtualTerminal();
        }

        try
        {
            Console.OutputEncoding = utf8;
        }
        catch (IOException)
        {
        }

        try
        {
            // Ctrl+C arrives as an ordinary key event, so the application can shut down cleanly.
            Console.TreatControlCAsInput = true;
        }
        catch (IOException)
        {
        }

        // A last line of defence if the process is killed by something other than a managed exception.
        AppDomain.CurrentDomain.ProcessExit += onProcessExit;
        entered = true;
        write(enterSequence);
    }

    public void leave()
    {
        if (!entered)
        {
            return;
        }

        entered = false;
        AppDomain.CurrentDomain.ProcessExit -= onProcessExit;
        write(leaveSequence);
        try
        {
            Console.TreatControlCAsInput = false;
        }
        catch (IOException)
        {
        }
    }

    public void write(string text)
    {
        byte[] bytes = utf8.GetBytes(text);
        stdout.Write(bytes, 0, bytes.Length);
        stdout.Flush();
    }

    public void pollKeys(List<KeyEvent> output)
    {
        if (Console.IsInputRedirected)
        {
            return;
        }

        while (Console.KeyAvailable)
        {
            KeyEvent? mapped = map(Console.ReadKey(intercept: true));
            if (mapped is not null)
            {
                output.Add(mapped.Value);
            }
        }
    }

    public void Dispose() => leave();

    private void onProcessExit(object? sender, EventArgs args) => leave();

    private static int queryWindow(Func<int> query, int fallback)
    {
        try
        {
            int value = query();
            return value > 0 ? value : fallback;
        }
        catch (IOException)
        {
            return fallback;
        }
    }

    private static KeyEvent? map(ConsoleKeyInfo info)
    {
        KeyModifiers modifiers = KeyModifiers.None;
        if ((info.Modifiers & ConsoleModifiers.Shift) != 0)
        {
            modifiers |= KeyModifiers.Shift;
        }

        if ((info.Modifiers & ConsoleModifiers.Alt) != 0)
        {
            modifiers |= KeyModifiers.Alt;
        }

        if ((info.Modifiers & ConsoleModifiers.Control) != 0)
        {
            modifiers |= KeyModifiers.Control;
        }

        // Ctrl+letter reports a control character in KeyChar; recover the letter from the key code.
        if ((modifiers & KeyModifiers.Control) != 0 && info.Key >= ConsoleKey.A && info.Key <= ConsoleKey.Z)
        {
            return KeyEvent.ofChar((char)('a' + (info.Key - ConsoleKey.A)), modifiers);
        }

        Key? special = info.Key switch
        {
            ConsoleKey.UpArrow => Key.Up,
            ConsoleKey.DownArrow => Key.Down,
            ConsoleKey.LeftArrow => Key.Left,
            ConsoleKey.RightArrow => Key.Right,
            ConsoleKey.Enter => Key.Enter,
            ConsoleKey.Escape => Key.Escape,
            ConsoleKey.Backspace => Key.Backspace,
            ConsoleKey.Tab => Key.Tab,
            ConsoleKey.Home => Key.Home,
            ConsoleKey.End => Key.End,
            ConsoleKey.PageUp => Key.PageUp,
            ConsoleKey.PageDown => Key.PageDown,
            ConsoleKey.Insert => Key.Insert,
            ConsoleKey.Delete => Key.Delete,
            >= ConsoleKey.F1 and <= ConsoleKey.F12 => Key.F1 + (info.Key - ConsoleKey.F1),
            _ => null,
        };

        if (special is not null)
        {
            return KeyEvent.ofKey(special.Value, modifiers);
        }

        return info.KeyChar >= ' ' && info.KeyChar != '\u007f' ? KeyEvent.ofChar(info.KeyChar, modifiers) : null;
    }

    private static void enableWindowsVirtualTerminal()
    {
        const int stdOutputHandle = -11;
        const uint enableVirtualTerminalProcessing = 0x0004;

        IntPtr handle = getStdHandle(stdOutputHandle);
        if (getConsoleMode(handle, out uint mode))
        {
            setConsoleMode(handle, mode | enableVirtualTerminalProcessing);
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetStdHandle")]
    private static extern IntPtr getStdHandle(int handle);

    [DllImport("kernel32.dll", EntryPoint = "GetConsoleMode")]
    private static extern bool getConsoleMode(IntPtr handle, out uint mode);

    [DllImport("kernel32.dll", EntryPoint = "SetConsoleMode")]
    private static extern bool setConsoleMode(IntPtr handle, uint mode);
}
