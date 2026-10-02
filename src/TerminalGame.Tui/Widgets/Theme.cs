namespace TerminalGame.Tui.Widgets;

/// <summary>The palette widgets draw with. Defaults evoke the classic blue JRPG window.</summary>
public sealed record Theme(
    Style text,
    Style panel,
    Style border,
    Style title,
    Style selection,
    Style selectionInactive,
    Style disabled,
    Color accent,
    Color progressTrack)
{
    public static readonly Theme defaultTheme = createDefault();

    private static Theme createDefault()
    {
        Color panelBlue = Color.rgb(18, 28, 96);
        Color ink = Color.rgb(236, 236, 240);

        return new Theme(
            text: Style.of(ink, Color.transparent),
            panel: Style.of(ink, panelBlue),
            border: Style.of(Color.rgb(220, 220, 235), panelBlue),
            title: Style.of(Color.gold, panelBlue, TextAttributes.Bold),
            selection: Style.of(Color.rgb(255, 235, 140), Color.rgb(52, 76, 170), TextAttributes.Bold),
            selectionInactive: Style.of(Color.rgb(190, 190, 205), Color.rgb(34, 48, 120)),
            disabled: Style.of(Color.rgb(120, 124, 150), Color.transparent, TextAttributes.Dim),
            accent: Color.rgb(70, 180, 90),
            progressTrack: Color.rgb(40, 44, 70));
    }
}
