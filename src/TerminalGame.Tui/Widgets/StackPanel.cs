namespace TerminalGame.Tui.Widgets;

public enum Orientation
{
    Vertical,
    Horizontal,
}

/// <summary>
/// Lays children out in a row or column. Along the main axis each child's <see cref="Widget.layoutWidth"/> /
/// <see cref="Widget.layoutHeight"/> decides: Auto uses the measured size, Fixed an exact size, Fill shares the
/// remainder by weight (integer cells, leftover distributed to the earliest fill children). Across the
/// axis children stretch, unless they ask for a Fixed size.
/// </summary>
public sealed class StackPanel : Container
{
    public StackPanel(Orientation orientation = Orientation.Vertical, int spacing = 0)
    {
        this.orientation = orientation;
        this.spacing = spacing;
    }

    public Orientation orientation { get; }

    /// <summary>Empty cells between adjacent children.</summary>
    public int spacing { get; set; }

    protected override Size measureContent(Size available)
    {
        int main = 0;
        int cross = 0;
        int shown = 0;
        foreach (Widget child in children)
        {
            if (!child.visible)
            {
                continue;
            }

            Size desired = child.measure(available);
            int childMain = mainOf(desired);
            Length length = mainLength(child);
            main += length.kind == LengthKind.Fill ? 0 : (length.kind == LengthKind.Fixed ? length.value : childMain);
            cross = Math.Max(cross, crossOf(desired));
            shown++;
        }

        main += Math.Max(0, shown - 1) * spacing;
        return orientation == Orientation.Vertical ? new Size(cross, main) : new Size(main, cross);
    }

    public override void arrange(Rect rect)
    {
        base.arrange(rect);

        List<Widget> shown = children.Where(c => c.visible).ToList();
        int totalMain = orientation == Orientation.Vertical ? rect.height : rect.width;
        int totalCross = orientation == Orientation.Vertical ? rect.width : rect.height;

        int[] sizes = new int[shown.Count];
        int[] weights = new int[shown.Count];
        int used = Math.Max(0, shown.Count - 1) * spacing;
        int totalWeight = 0;
        for (int i = 0; i < shown.Count; i++)
        {
            Length length = mainLength(shown[i]);
            switch (length.kind)
            {
                case LengthKind.Fixed:
                    sizes[i] = length.value;
                    break;
                case LengthKind.Auto:
                    Size available = orientation == Orientation.Vertical
                        ? new Size(totalCross, totalMain)
                        : new Size(totalMain, totalCross);
                    sizes[i] = mainOf(shown[i].measure(available));
                    break;
                default:
                    weights[i] = length.value;
                    totalWeight += length.value;
                    break;
            }

            used += sizes[i];
        }

        int remaining = Math.Max(0, totalMain - used);
        if (totalWeight > 0)
        {
            distribute(sizes, weights, remaining, totalWeight);
        }

        int cursor = 0;
        for (int i = 0; i < shown.Count; i++)
        {
            int size = Math.Max(0, Math.Min(sizes[i], totalMain - cursor));
            Length crossLength = crossLengthOf(shown[i]);
            int cross = crossLength.kind == LengthKind.Fixed ? Math.Min(crossLength.value, totalCross) : totalCross;

            Rect childRect = orientation == Orientation.Vertical
                ? new Rect(0, cursor, cross, size)
                : new Rect(cursor, 0, size, cross);
            shown[i].arrange(childRect);
            cursor += size + spacing;
        }
    }

    private static void distribute(int[] sizes, int[] weights, int remaining, int totalWeight)
    {
        // Floor each share, then hand the leftover cells out one at a time, earliest fill child first.
        int assigned = 0;
        List<int> fillIndexes = new();
        for (int i = 0; i < sizes.Length; i++)
        {
            if (weights[i] > 0)
            {
                sizes[i] = remaining * weights[i] / totalWeight;
                assigned += sizes[i];
                fillIndexes.Add(i);
            }
        }

        for (int k = 0; assigned < remaining; k++, assigned++)
        {
            sizes[fillIndexes[k % fillIndexes.Count]]++;
        }
    }

    private Length mainLength(Widget child) => orientation == Orientation.Vertical ? child.layoutHeight : child.layoutWidth;

    private Length crossLengthOf(Widget child) => orientation == Orientation.Vertical ? child.layoutWidth : child.layoutHeight;

    private int mainOf(Size size) => orientation == Orientation.Vertical ? size.height : size.width;

    private int crossOf(Size size) => orientation == Orientation.Vertical ? size.width : size.height;
}
