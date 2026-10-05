using TerminalGame.Demo;
using TerminalGame.Rpg.Data;
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
app.run(new TitleScene(content));

// Plays a scripted session on an in-memory terminal and prints the screen after each step.
// Runs anywhere (CI, no TTY), and shows how scenes can be tested without a real terminal.
static void runSnapshot(ContentDb content)
{
    HeadlessBackend backend = new(80, 24);
    Application app = new(backend);
    app.pushScene(new TitleScene(content, seed: 1));

    void show(string label, double seconds = 1.0)
    {
        app.step(seconds);
        Console.WriteLine($"===== {label} =====");
        Console.Write(backend.screenText);
    }

    void press(params Key[] keys)
    {
        backend.queueKeys(keys);
        app.step(0.0);
    }

    show("title");
    press(Key.Enter);
    show("town: first line typing", 0.5);
    show("town: first line complete", 5);
    press(Key.Enter); // next message
    show("town: second line complete", 5);
    press(Key.Enter); // dismiss -> choice popup
    show("town: choices");
    press(Key.Enter); // "go to the forest"
    show("battle: intro typing", 0.4);
    show("battle: intro complete", 5);
    press(Key.Enter); // dismiss -> player's turn
    show("battle: command menu");
    press(Key.Down, Key.Enter); // skills
    show("battle: skill list");
    press(Key.Enter); // fireball
    show("battle: fireball message", 5);
    press(Key.Enter);
    show("battle: enemy turn", 5);
    press(Key.Enter);
    show("battle: back to commands");
}
