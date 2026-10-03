using TerminalGame.Tui.Backends;
using TerminalGame.Tui.Input;
using TerminalGame.Tui.Runtime;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Tui.Tests;

public class RuntimeTests
{
    private const double frame = 1.0 / 30;

    private sealed class RecordingScene : Scene
    {
        private readonly string name;
        private readonly List<string> log;

        public RecordingScene(string name, List<string> log, bool transparent = false)
        {
            this.name = name;
            this.log = log;
            isTransparentScene = transparent;
        }

        public bool isTransparentScene { get; }

        public override bool isTransparent => isTransparentScene;

        public List<KeyEvent> keys { get; } = new();

        public override void onEnter() => log.Add($"{name}:enter");

        public override void onExit() => log.Add($"{name}:exit");

        public override void onPause() => log.Add($"{name}:pause");

        public override void onResume() => log.Add($"{name}:resume");

        public override void onKey(KeyEvent keyEvent) => keys.Add(keyEvent);

        public override void onResize(Size newSize) => log.Add($"{name}:resize {newSize.width}x{newSize.height}");
    }

    private sealed class LabelScene : Scene
    {
        public LabelScene(string text, bool transparent = false)
        {
            root = new Label(text);
            isTransparentScene = transparent;
        }

        private bool isTransparentScene { get; }

        public override bool isTransparent => isTransparentScene;
    }

    [Fact]
    public void sceneLifecycleFollowsStackOperations()
    {
        HeadlessBackend backend = new();
        Application app = new(backend);
        List<string> log = new();
        RecordingScene a = new("A", log);
        RecordingScene b = new("B", log);
        RecordingScene c = new("C", log);

        app.pushScene(a);
        app.step(frame);
        app.pushScene(b);
        app.step(frame);
        app.replaceScene(c);
        app.step(frame);
        app.popScene();
        app.step(frame);

        Assert.Equal(new[] { "A:enter", "A:pause", "B:enter", "B:exit", "C:enter", "C:exit", "A:resume" }, log);
        Assert.Same(a, app.currentScene);
        Assert.Null(b.application);
        Assert.Same(app, a.application);
    }

    [Fact]
    public void poppingTheLastSceneStopsTheApplication()
    {
        Application app = new(new HeadlessBackend());
        app.pushScene(new RecordingScene("A", new List<string>()));
        app.step(frame);
        app.popScene();
        app.step(frame);
        Assert.Equal(0, app.sceneCount);
        Assert.False(app.isRunning);
    }

    [Fact]
    public void sceneChangesRequestedDuringCallbacksAreDeferred()
    {
        HeadlessBackend backend = new();
        Application app = new(backend);
        List<string> log = new();
        RecordingScene second = new("second", log);

        PushOnKeyScene first = new(app, second);
        app.pushScene(first);
        app.step(frame);
        backend.queueKeys(Key.Enter);
        app.step(frame);

        Assert.Same(first, app.currentScene); // not applied mid-frame
        app.step(frame);
        Assert.Same(second, app.currentScene);
    }

    private sealed class PushOnKeyScene : Scene
    {
        private readonly Application owner;
        private readonly Scene next;

        public PushOnKeyScene(Application owner, Scene next)
        {
            this.owner = owner;
            this.next = next;
        }

        public override void onKey(KeyEvent keyEvent) => owner.pushScene(next);
    }

    [Fact]
    public void keysResolveToActionsThroughTheKeyMap()
    {
        HeadlessBackend backend = new();
        Application app = new(backend);
        RecordingScene scene = new("s", new List<string>());
        app.pushScene(scene);
        backend.queueKeys(Key.Up, Key.Enter, Key.Escape);
        backend.typeText("zx?");
        app.step(frame);

        Assert.Equal(
            new[] { GameAction.Up, GameAction.Confirm, GameAction.Cancel, GameAction.Confirm, GameAction.Cancel, GameAction.None },
            scene.keys.Select(k => k.action).ToArray());
    }

    [Fact]
    public void keyMapCanBeRebound()
    {
        HeadlessBackend backend = new();
        ApplicationOptions options = new() { keyMap = KeyMap.createDefault().bindChar(GameAction.Up, 'w') };
        Application app = new(backend, options);
        RecordingScene scene = new("s", new List<string>());
        app.pushScene(scene);
        backend.typeText("W");
        app.step(frame);
        Assert.Equal(GameAction.Up, scene.keys.Single().action);
    }

    [Fact]
    public void ctrlCQuits()
    {
        HeadlessBackend backend = new();
        Application app = new(backend);
        app.pushScene(new RecordingScene("s", new List<string>()));
        app.step(frame);
        backend.queueKey(KeyEvent.ofChar('c', KeyModifiers.Control));
        app.step(frame);
        Assert.False(app.isRunning);
    }

    private sealed class MenuScene : Scene
    {
        public MenuScene()
        {
            menu = new MenuList(new[] { MenuItem.of("one"), MenuItem.of("two"), MenuItem.of("three") });
            root = new Border(menu, "Menu");
            setFocus(menu);
        }

        public MenuList menu { get; }

        public List<KeyEvent> unhandled { get; } = new();

        public override void onKey(KeyEvent keyEvent) => unhandled.Add(keyEvent);
    }

    [Fact]
    public void focusedWidgetGetsInputAndUnhandledKeysFallThroughToTheScene()
    {
        HeadlessBackend backend = new();
        Application app = new(backend);
        MenuScene scene = new();
        List<int> confirmed = new();
        scene.menu.confirmed += confirmed.Add;
        app.pushScene(scene);

        backend.queueKeys(Key.Down, Key.Enter, Key.Escape, Key.F1);
        app.step(frame);

        Assert.Equal(new[] { 1 }, confirmed);
        Assert.Equal(new[] { Key.Escape, Key.F1 }, scene.unhandled.Select(k => k.key).ToArray()); // Esc: menu has no cancel handler
        Assert.True(scene.menu.focused);
    }

    [Fact]
    public void modalCapturesInputAndRestoresFocusOnClose()
    {
        HeadlessBackend backend = new();
        Application app = new(backend);
        MenuScene scene = new();
        app.pushScene(scene);
        app.step(frame);

        MenuList popup = new(new[] { MenuItem.of("yes"), MenuItem.of("no") });
        List<int> answers = new();
        popup.confirmed += answers.Add;
        scene.showModal(new Border(popup, "Sure?"));
        Assert.True(popup.focused);
        Assert.False(scene.menu.focused);

        backend.queueKeys(Key.Down, Key.Enter, Key.F2);
        app.step(frame);
        Assert.Equal(new[] { 1 }, answers);
        Assert.Empty(scene.unhandled); // F2 was swallowed by the modal, not leaked to the scene
        Assert.Equal(0, scene.menu.selectedIndex);

        scene.closeModal((Border)popup.parent!);
        Assert.True(scene.menu.focused);
        Assert.False(scene.hasModal);
    }

    [Fact]
    public void modalIsDrawnCenteredAboveTheScene()
    {
        HeadlessBackend backend = new(20, 7);
        Application app = new(backend);
        MenuScene scene = new();
        app.pushScene(scene);
        scene.showModal(new Border(new Label("OK"), null));
        app.step(frame);

        string[] lines = backend.screenText.Split('\n');
        Assert.Contains("┌", lines[2]);
        Assert.Contains("OK", lines[3]);
        Assert.Contains("┘", lines[4]);
    }

    [Fact]
    public void anUnchangedSceneWritesNothingAfterTheFirstFrame()
    {
        HeadlessBackend backend = new();
        Application app = new(backend);
        app.pushScene(new LabelScene("static"));
        app.step(frame);
        int writes = backend.writeCount;
        Assert.True(writes > 0);
        app.step(frame);
        app.step(frame);
        Assert.Equal(writes, backend.writeCount);
        Assert.StartsWith("static", backend.screenText);
    }

    [Fact]
    public void transparentSceneShowsTheOneBelow()
    {
        HeadlessBackend backend = new(20, 3);
        Application app = new(backend);
        app.pushScene(new LabelScene("background"));
        app.pushScene(new OverlayScene());
        app.step(frame);
        app.step(frame);

        string first = backend.screenText.Split('\n')[0];
        Assert.StartsWith("background", first);

        app.pushScene(new LabelScene("opaque"));
        app.step(frame);
        app.step(frame);
        Assert.DoesNotContain("background", backend.screenText);
    }

    private sealed class OverlayScene : Scene
    {
        public OverlayScene()
        {
            root = new StackPanel(Orientation.Vertical);
            ((StackPanel)root).add(new Spacer());
            ((StackPanel)root).add(new Label("overlay"));
        }

        public override bool isTransparent => true;
    }

    [Fact]
    public void resizeRepaintsEverythingAndNotifiesScenes()
    {
        HeadlessBackend backend = new(20, 3);
        Application app = new(backend);
        List<string> log = new();
        app.pushScene(new RecordingScene("s", log));
        app.step(frame);

        backend.resize(30, 5);
        app.step(frame);
        Assert.Contains("s:resize 30x5", log);
        Assert.Equal(new Size(30, 5), app.screenSize);
    }

    [Fact]
    public void resizeKeepsRenderingCorrectly()
    {
        HeadlessBackend backend = new(20, 3);
        Application app = new(backend);
        app.pushScene(new LabelScene("hello"));
        app.step(frame);
        backend.resize(10, 2);
        app.step(frame);
        Assert.StartsWith("hello", backend.screenText);
    }

    [Fact]
    public void updateDeltaReachesWidgets()
    {
        HeadlessBackend backend = new(30, 6);
        Application app = new(backend);
        DialogueScene scene = new();
        app.pushScene(scene);
        app.step(0.0);
        scene.box.show("Hello");
        app.step(0.25);
        Assert.Contains("He", backend.screenText);
        Assert.DoesNotContain("Hello", backend.screenText);
        app.step(1.0);
        Assert.Contains("Hello", backend.screenText);
    }

    private sealed class DialogueScene : Scene
    {
        public DialogueScene()
        {
            box = new DialogueBox { charsPerSecond = 10, punctuationPause = 0 };
            root = box;
            setFocus(box);
        }

        public DialogueBox box { get; }
    }

    [Fact]
    public void aThrowingSceneStillLeavesTheTerminalRestored()
    {
        HeadlessBackend backend = new();
        Application app = new(backend);
        Assert.Throws<InvalidOperationException>(() => app.run(new ThrowingScene()));
        Assert.False(backend.isEntered);
    }

    private sealed class ThrowingScene : Scene
    {
        public override void onUpdate(double deltaSeconds) => throw new InvalidOperationException("boom");
    }

    [Fact]
    public void runLoopTerminatesWhenSceneRequestsQuit()
    {
        HeadlessBackend backend = new();
        Application app = new(backend, new ApplicationOptions { targetFps = 200 });
        QuitAfterScene scene = new(app, frames: 3);
        app.run(scene);
        Assert.Equal(3, scene.frames);
        Assert.False(backend.isEntered);
    }

    private sealed class QuitAfterScene : Scene
    {
        private readonly Application owner;
        private readonly int limit;

        public QuitAfterScene(Application owner, int frames)
        {
            this.owner = owner;
            limit = frames;
        }

        public int frames { get; private set; }

        public override void onUpdate(double deltaSeconds)
        {
            frames++;
            if (frames >= limit)
            {
                owner.quit();
            }
        }
    }
}
