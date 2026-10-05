using TerminalGame.Tui;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Widgets;

namespace TerminalGame.Demo;

/// <summary>
/// Window colors per region (LocationDef.palette). Changing the panel color is the cheapest way to make each
/// area feel different: blue town windows, green forest, near-black deep woods.
/// </summary>
public static class Palettes
{
    private static readonly Dictionary<string, Theme> themes = new()
    {
        ["town"] = Theme.defaultTheme,
        ["forest"] = tinted(Color.rgb(16, 52, 30), Color.rgb(170, 215, 150), Color.rgb(40, 96, 56)),
        ["deepForest"] = tinted(Color.rgb(16, 22, 24), Color.rgb(120, 170, 130), Color.rgb(36, 62, 52)),
        ["cave"] = tinted(Color.rgb(24, 24, 34), Color.rgb(170, 170, 190), Color.rgb(52, 52, 80)),
    };

    public static Theme forName(string? name) =>
        name is not null && themes.TryGetValue(name, out Theme? theme) ? theme : Theme.defaultTheme;

    private static Theme tinted(Color panel, Color frame, Color highlight)
    {
        Theme d = Theme.defaultTheme;
        return d with
        {
            panel = d.panel with { background = panel },
            border = Style.of(frame, panel),
            title = d.title with { background = panel },
            selection = d.selection with { background = highlight },
            selectionInactive = d.selectionInactive with { background = panel },
        };
    }
}
