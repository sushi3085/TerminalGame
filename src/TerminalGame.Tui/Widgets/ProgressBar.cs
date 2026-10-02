using System.Globalization;
using TerminalGame.Tui.Rendering;
using TerminalGame.Tui.Text;

namespace TerminalGame.Tui.Widgets;

/// <summary>
/// A one-row gauge (HP, MP, ATB…). The filled part is painted as background color, so the label can be drawn
/// straight over it; the boundary cell uses a left-block glyph (▏…▉) for 1/8-cell resolution.
/// </summary>
public sealed class ProgressBar : Widget
{
    private static readonly string[] eighths = { " ", "▏", "▎", "▍", "▌", "▋", "▊", "▉" };

    private Color[] gradient = Array.Empty<Color>();

    public ProgressBar(double value = 0, double maximum = 100, string caption = "")
    {
        this.value = value;
        this.maximum = maximum;
        this.caption = caption;
    }

    public double value { get; set; }

    public double maximum { get; set; }

    /// <summary>Shown before the numbers, e.g. "HP".</summary>
    public string caption { get; set; }

    public bool showNumbers { get; set; } = true;

    /// <summary>Fixed fill color; transparent means "theme accent".</summary>
    public Color fillColor { get; set; }

    public double ratio => maximum <= 0 ? 0 : Math.Clamp(value / maximum, 0, 1);

    /// <summary>Colors evenly spread over 0%..100%, interpolated at the current ratio (e.g. red, yellow, green).</summary>
    public void setGradient(params Color[] stops) => gradient = stops;

    protected override Size measureContent(Size available) => new(Math.Min(available.width, 16), 1);

    public override void render(Canvas canvas)
    {
        int width = canvas.size.width;
        if (width <= 0 || canvas.size.height <= 0)
        {
            return;
        }

        Theme theme = effectiveTheme;
        Color fill = resolveFill(theme);
        Color track = theme.progressTrack;

        int totalEighths = (int)Math.Round(ratio * width * 8);
        int full = totalEighths / 8;
        int partial = totalEighths % 8;

        for (int x = 0; x < width; x++)
        {
            if (x < full)
            {
                canvas.putGlyph(x, 0, " ", 1, Style.of(Color.transparent, fill));
            }
            else if (x == full && partial > 0)
            {
                canvas.putGlyph(x, 0, eighths[partial], 1, Style.of(fill, track));
            }
            else
            {
                canvas.putGlyph(x, 0, " ", 1, Style.of(Color.transparent, track));
            }
        }

        string label = buildLabel();
        if (label.Length > 0)
        {
            int labelWidth = UnicodeWidth.ofString(label);
            int start = Math.Max(0, (width - labelWidth) / 2);
            canvas.drawText(start, 0, label, Style.of(Color.white, Color.transparent, TextAttributes.Bold));
        }
    }

    private string buildLabel()
    {
        string numbers = showNumbers
            ? string.Create(CultureInfo.InvariantCulture, $"{Math.Round(value)}/{Math.Round(maximum)}")
            : string.Empty;
        return caption.Length > 0 && numbers.Length > 0 ? caption + " " + numbers : caption + numbers;
    }

    private Color resolveFill(Theme theme)
    {
        if (gradient.Length == 0)
        {
            return fillColor.isTransparent ? theme.accent : fillColor;
        }

        if (gradient.Length == 1)
        {
            return gradient[0];
        }

        double scaled = ratio * (gradient.Length - 1);
        int index = Math.Min((int)scaled, gradient.Length - 2);
        return gradient[index].lerp(gradient[index + 1], scaled - index);
    }
}
