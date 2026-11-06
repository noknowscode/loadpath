using Loadpath.Controls;
using SkiaSharp;

namespace Loadpath.Workspace;

/// <summary>
/// The canvas palette for one theme: the Simple theme's role colours (the design's palette, Styles/ColorPaletteOverride.xaml),
/// App.xaml's Warn and the per-theme alphas Skia bakes into its paints. Construction lines and grid marks are Primary
/// blended into the paper. Immutable: the view builds a new one on load, on every ActualThemeChanged and when the palette
/// itself changes (ThemeLookup.PaletteChanged), and hands it to the renderer, which swaps it in at the start of the next frame.
/// </summary>
public sealed class SkiaPalette
{
    /// <summary>The paper (Background).</summary>
    public required SKColor Background { get; init; }
    /// <summary>Secondary ink (OnSurfaceVariant): unsolved members, the drawing's caption.</summary>
    public required SKColor OnSurfaceVariant { get; init; }
    /// <summary>The blueprint ink (Primary): the structure, its forces, supports, loads and annotations.</summary>
    public required SKColor Primary { get; init; }
    /// <summary>The selection wash (PrimaryContainer).</summary>
    public required SKColor PrimaryContainer { get; init; }
    /// <summary>Construction lines: frame, grid crosses, extension lines (Primary blended into the paper).</summary>
    public required SKColor Construction { get; init; }
    /// <summary>Minor grid dots (Primary blended into the paper, fainter).</summary>
    public required SKColor GridMark { get; init; }
    /// <summary>Over capacity (Error) and its plate (ErrorContainer).</summary>
    public required SKColor Error { get; init; }
    public required SKColor ErrorContainer { get; init; }
    /// <summary>The amber step of the utilization ramp (App.xaml, no palette role).</summary>
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

    /// <summary>Read the palette for a theme (Light or Dark) from the theme's role colours and App.xaml.</summary>
    /// <remarks>A key that does not resolve draws transparent (or fully opaque, for an alpha): visibly wrong, never a stale constant.</remarks>
    public static SkiaPalette FromResources(ElementTheme theme)
    {
        SKColor C(string key)
        {
            var c = ThemeLookup.Color(key, theme, Microsoft.UI.Colors.Transparent);
            return new SKColor(c.R, c.G, c.B, c.A);
        }
        double D(string key) => ThemeLookup.Double(key, theme, 1.0);
        // The role brushes, not the palette's colours: ThemeLookup watches a brush it hands out, so a palette edit
        // that recolours them raises ThemeLookup.PaletteChanged and the view reads a new palette.
        var background = C("BackgroundBrush");
        var primary = C("PrimaryBrush");
        return new SkiaPalette
        {
            Background = background,
            OnSurfaceVariant = C("OnSurfaceVariantBrush"),
            Primary = primary,
            PrimaryContainer = C("PrimaryContainerBrush"),
            Construction = Lerp(background, primary, D("CanvasConstructionAlpha")),
            GridMark = Lerp(background, primary, D("CanvasGridAlpha")),
            Error = C("ErrorBrush"),
            ErrorContainer = C("ErrorContainerBrush"),
            Warn = C("WarnBrush"),
            DimAlpha = D("CanvasDimAlpha"),
            DimInkAlpha = D("CanvasDimInkAlpha"),
            HaloAlpha = D("CanvasHaloAlpha"),
            PlateAlpha = D("CanvasPlateAlpha"),
            MarqueeAlpha = D("CanvasMarqueeAlpha"),
            DimLineAlpha = D("CanvasDimLineAlpha"),
            TagAlpha = D("CanvasTagAlpha"),
        };
    }

    /// <summary>Utilization ramp on paper: Primary → Warn → Error, saturating at 1.</summary>
    public SKColor Utilization(double u)
    {
        u = Math.Clamp(u, 0, 1);
        return u < 0.5 ? Lerp(Primary, Warn, u / 0.5) : Lerp(Warn, Error, (u - 0.5) / 0.5);
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
