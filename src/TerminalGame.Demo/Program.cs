using TerminalGame.Demo;
using TerminalGame.Rpg.Data;
using TerminalGame.Rpg.State;
using TerminalGame.Tui.Backends;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;

ContentDb content;
try
{
    content = ContentDb.loadDirectory(Path.Combine(AppContext.BaseDirectory, "content"));
}
catch (ContentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return;
}

if (args.Contains("--snapshot"))
{
    runSnapshot(content);
    return;
}

using Application app = new(new ConsoleBackend());
app.run(new TitleScene(new Game(content, SaveStore.userDefault())));

// Plays a scripted session on an in-memory terminal and prints the screen after each step.
// Runs anywhere (CI, no TTY), and shows how scenes can be tested without a real terminal.
static void runSnapshot(ContentDb content)
{
    HeadlessBackend backend = new(80, 24);
    Application app = new(backend);
    string saveDirectory = Path.Combine(Path.GetTempPath(), "TerminalGame-snapshot-" + Environment.ProcessId);
    app.pushScene(new TitleScene(new Game(content, new SaveStore(saveDirectory), seed: 1)));

    void show(string label, double seconds = 1.0)
    {
        app.step(seconds);
        Console.WriteLine($"===== {label} =====");
        Console.Write(backend.screenText);
    }

    void press(params Key[] keys)
    {
        foreach (Key key in keys)
        {
            backend.queueKeys(key);
            app.step(0.0);
        }
    }

    // Dismisses messages (fully typing each one) until the screen shows the given text.
    void pressEnterUntil(string text)
    {
        for (int i = 0; i < 60 && !backend.screenText.Contains(text); i++)
        {
            app.step(5);
            press(Key.Enter);
            app.step(5);
        }
    }

    show("title");
    press(Key.Enter); // new game
    show("town: arrival typing", 0.5);
    show("town: arrival", 5);
    press(Key.Enter);
    show("town: elder", 5);
    pressEnterUntil("▶ 長老家");
    show("town: actions");
    press(Key.Tab);
    show("party menu");
    press(Key.Escape, Key.Down, Key.Down, Key.Down, Key.Down, Key.Down, Key.Down, Key.Enter); // leave for the forest
    show("woods: arrival", 5);
    pressEnterUntil("▶ 調查樹樁");
    press(Key.Down, Key.Down, Key.Down, Key.Enter); // search the area
    pressEnterUntil("▶ 攻擊");
    show("battle: command menu");
    press(Key.Down, Key.Enter); // skills
    show("battle: skill list");
    press(Key.Enter); // fireball
    show("battle: fireball", 5);

    if (Directory.Exists(saveDirectory))
    {
        Directory.Delete(saveDirectory, recursive: true);
    }
}
