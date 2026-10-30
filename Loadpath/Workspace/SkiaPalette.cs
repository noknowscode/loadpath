using Loadpath.Controls;
using SkiaSharp;

namespace Loadpath.Workspace;

/// <summary>
/// The canvas palette for one theme, read from Themes/Tokens.xaml (colors and the per-theme alphas Skia bakes into its
/// paints). Immutable: the view builds a new one on load and on every ActualThemeChanged and hands it to the renderer,
/// which swaps it in at the start of the next frame.
/// </summary>
public sealed class SkiaPalette
{
    public required SKColor Canvas { get; init; }
    public required SKColor Ink { get; init; }
    public required SKColor InkSecondary { get; init; }
    public required SKColor InkTertiary { get; init; }
    /// <summary>Blueprint ink: the structure, its annotations and the accent.</summary>
    public required SKColor Accent { get; init; }
    public required SKColor AccentSoft { get; init; }
    /// <summary>Construction lines: frame, grid marks, extension lines.</summary>
    public required SKColor AccentFaint { get; init; }
    public required SKColor GridMark { get; init; }
    public required SKColor Danger { get; init; }
    public required SKColor DangerSoft { get; init; }
    public required SKColor Warn { get; init; }

    /// <summary>Dimmed members (a single member selected, or a detached part).</summary>
    public required double DimAlpha { get; init; }
    /// <summary>Dimmed nodes, supports and loads.</summary>
    public required double DimInkAlpha { get; init; }
    /// <summary>Hover halos, guides and the selected node's dashed ring.</summary>
    public required double HaloAlpha { get; init; }
    /// <summary>The paper plate behind a force label.</summary>
    public required double PlateAlpha { get; init; }
    public required double MarqueeAlpha { get; init; }
    public required double DimLineAlpha { get; init; }
    public required double TagAlpha { get; init; }

    /// <summary>Read the palette for a theme (Light or Dark) from the token dictionaries.</summary>
    /// <remarks>A key that does not resolve draws transparent (or fully opaque, for an alpha): visibly wrong, never a stale constant.</remarks>
    public static SkiaPalette FromResources(ElementTheme theme)
    {
        SKColor C(string key)
        {
            var c = ThemeLookup.Color(key, theme, Microsoft.UI.Colors.Transparent);
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        double D(string key) => ThemeLookup.Double(key, theme, 1.0);
        return new SkiaPalette
        {
            Canvas = C("CanvasColor"),
            Ink = C("InkColor"),
            InkSecondary = C("InkSecondaryColor"),
            InkTertiary = C("InkTertiaryColor"),
            Accent = C("AccentColor"),
            AccentSoft = C("AccentSoftColor"),
            AccentFaint = C("ConstructionColor"),
            GridMark = C("GridMarkColor"),
            Danger = C("DangerColor"),
            DangerSoft = C("DangerSoftColor"),
            Warn = C("WarnColor"),
            DimAlpha = D("CanvasDimAlpha"),
            DimInkAlpha = D("CanvasDimInkAlpha"),
            HaloAlpha = D("CanvasHaloAlpha"),
            PlateAlpha = D("CanvasPlateAlpha"),
            MarqueeAlpha = D("CanvasMarqueeAlpha"),
            DimLineAlpha = D("CanvasDimLineAlpha"),
            TagAlpha = D("CanvasTagAlpha"),
        };
    }

    /// <summary>Utilization ramp on paper: blueprint → amber → red, saturating at 1.</summary>
    public SKColor Utilization(double u)
    {
        u = Math.Clamp(u, 0, 1);
        return u < 0.5 ? Lerp(Accent, Warn, u / 0.5) : Lerp(Warn, Danger, (u - 0.5) / 0.5);
    }

    public static SKColor Lerp(SKColor a, SKColor b, double t)
    {
        t = Math.Clamp(t, 0, 1);
        return new SKColor(
            (byte)(a.Red + (b.Red - a.Red) * t),
            (byte)(a.Green + (b.Green - a.Green) * t),
            (byte)(a.Blue + (b.Blue - a.Blue) * t),
            (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    }
}

public static class SkiaColorExtensions
{
    public static SKColor WithAlpha(this SKColor c, double alpha) => c.WithAlpha((byte)Math.Clamp(alpha * 255, 0, 255));
}
