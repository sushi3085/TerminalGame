using TerminalGame.Tui.Rendering;

namespace TerminalGame.Tui.Widgets;

/// <summary>Draws nothing; occupies space in a stack. Fills by default so it pushes siblings apart.</summary>
public sealed class Spacer : Widget
{
    public Spacer(int weight = 1)
    {
        layoutWidth = Length.fill(weight);
        layoutHeight = Length.fill(weight);
    }

    public override void render(Canvas canvas)
    {
    }
}
