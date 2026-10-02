using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Tui.Tests;

internal static class TestHelpers
{
    /// <summary>Lays out and renders a widget into a fresh buffer.</summary>
    public static ScreenBuffer renderToBuffer(Widget widget, int width, int height)
    {
        ScreenBuffer buffer = new(width, height);
        widget.measure(new Size(width, height));
        widget.arrange(new Rect(0, 0, width, height));
        widget.render(new Canvas(buffer).sub(widget.bounds));
        return buffer;
    }

    public static string[] rows(ScreenBuffer buffer) =>
        Enumerable.Range(0, buffer.height).Select(buffer.rowToString).ToArray();

    public static string[] renderRows(Widget widget, int width, int height) =>
        rows(renderToBuffer(widget, width, height));
}
