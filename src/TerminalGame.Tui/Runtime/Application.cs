using System.Diagnostics;
using TerminalGame.Tui.Backends;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Rendering;

namespace TerminalGame.Tui.Runtime;

public sealed class ApplicationOptions
{
    /// <summary>Upper bound for the frame rate; the loop sleeps away the rest of each frame.</summary>
    public int targetFps { get; set; } = 30;

    public KeyMap keyMap { get; set; } = KeyMap.createDefault();

    /// <summary>Treat Ctrl+C as "quit" (the console backend delivers it as a key event, not a signal).</summary>
    public bool quitOnCtrlC { get; set; } = true;

    /// <summary>Longest time step passed to updates, so a stall (debugger, window drag) does not fast-forward the game.</summary>
    public double maxDeltaSeconds { get; set; } = 0.25;
}

/// <summary>
/// The game host: owns the terminal backend, the scene stack and the frame loop.
/// <code>
/// using var app = new Application(new ConsoleBackend());
/// app.run(new TitleScene());
/// </code>
/// One frame = apply queued scene changes, poll input, dispatch to the top scene, update, render, diff, write.
/// Scene changes (<see cref="pushScene"/> etc.) are queued and applied at the start of the next frame, so a
/// scene may safely change the stack from inside its own callbacks.
/// </summary>
public sealed class Application : IDisposable
{
    private readonly ITerminalBackend backend;
    private readonly List<Scene> scenes = new();
    private readonly Queue<Action> pendingChanges = new();
    private readonly List<KeyEvent> keyBuffer = new();
    private readonly FrameRenderer renderer;
    private ScreenBuffer frame;
    private Size lastSize;
    private bool running;

    public Application(ITerminalBackend backend, ApplicationOptions? options = null)
    {
        this.backend = backend;
        this.options = options ?? new ApplicationOptions();
        renderer = new FrameRenderer(backend.colorMode);
        lastSize = new Size(backend.width, backend.height);
        frame = new ScreenBuffer(lastSize.width, lastSize.height);
    }

    public ApplicationOptions options { get; }

    public Size screenSize => lastSize;

    public Scene? currentScene => scenes.Count > 0 ? scenes[^1] : null;

    public int sceneCount => scenes.Count;

    public bool isRunning => running;

    public void pushScene(Scene scene) => pendingChanges.Enqueue(() => doPush(scene));

    /// <summary>Removes the top scene; the application quits when the stack becomes empty.</summary>
    public void popScene() => pendingChanges.Enqueue(doPop);

    /// <summary>Swaps the top scene. Scenes below it stay paused, so they see no lifecycle calls.</summary>
    public void replaceScene(Scene scene) => pendingChanges.Enqueue(() =>
    {
        exitTop();
        enter(scene);
    });

    public void quit() => running = false;

    /// <summary>
    /// Runs until <see cref="quit"/> or the scene stack empties. The terminal is always restored, even if a
    /// scene throws, so the exception message lands on a normal screen instead of being lost with the alt screen.
    /// </summary>
    public void run(Scene initial)
    {
        pushScene(initial);
        running = true;
        backend.enter();
        try
        {
            double frameSeconds = 1.0 / Math.Max(1, options.targetFps);
            Stopwatch clock = Stopwatch.StartNew();
            double previous = clock.Elapsed.TotalSeconds;

            while (running)
            {
                double now = clock.Elapsed.TotalSeconds;
                double delta = Math.Min(now - previous, options.maxDeltaSeconds);
                previous = now;

                step(delta);

                double spent = clock.Elapsed.TotalSeconds - now;
                int sleepMilliseconds = (int)((frameSeconds - spent) * 1000);
                if (sleepMilliseconds > 0)
                {
                    Thread.Sleep(sleepMilliseconds);
                }
            }
        }
        finally
        {
            backend.leave();
        }
    }

    /// <summary>
    /// Executes exactly one frame with the given time step. <see cref="run"/> calls this in a loop; tests and
    /// tools can call it directly with a fixed step for deterministic results.
    /// </summary>
    public void step(double deltaSeconds)
    {
        applyPendingChanges();
        if (scenes.Count == 0)
        {
            running = false;
            return;
        }

        syncSize();

        keyBuffer.Clear();
        backend.pollKeys(keyBuffer);
        foreach (KeyEvent raw in keyBuffer)
        {
            KeyEvent keyEvent = raw with { action = options.keyMap.resolve(raw) };
            if (options.quitOnCtrlC && keyEvent.isCtrl('c'))
            {
                quit();
                return;
            }

            currentScene?.dispatchKey(keyEvent);
        }

        currentScene?.tick(deltaSeconds);
        renderFrame();
    }

    public void Dispose() => backend.leave();

    private void applyPendingChanges()
    {
        while (pendingChanges.Count > 0)
        {
            pendingChanges.Dequeue().Invoke();
        }
    }

    private void doPush(Scene scene)
    {
        currentScene?.onPause();
        enter(scene);
    }

    private void doPop()
    {
        if (scenes.Count == 0)
        {
            return;
        }

        exitTop();
        if (scenes.Count > 0)
        {
            scenes[^1].onResume();
        }
        else
        {
            running = false;
        }
    }

    private void enter(Scene scene)
    {
        scene.application = this;
        scenes.Add(scene);
        scene.onEnter();
    }

    private void exitTop()
    {
        if (scenes.Count == 0)
        {
            return;
        }

        Scene top = scenes[^1];
        scenes.RemoveAt(scenes.Count - 1);
        top.onExit();
        top.application = null;
    }

    private void syncSize()
    {
        Size size = new(backend.width, backend.height);
        if (size == lastSize)
        {
            return;
        }

        lastSize = size;
        frame = new ScreenBuffer(size.width, size.height);
        renderer.invalidate();
        foreach (Scene scene in scenes)
        {
            scene.onResize(size);
        }
    }

    private void renderFrame()
    {
        frame.clear(Style.terminalDefault);
        Canvas canvas = new(frame);

        // Draw from the lowest scene that is not hidden behind an opaque one.
        int first = scenes.Count - 1;
        while (first > 0 && scenes[first].isTransparent)
        {
            first--;
        }

        for (int i = first; i < scenes.Count; i++)
        {
            scenes[i].renderTo(canvas);
        }

        backend.write(renderer.render(frame));
    }
}
