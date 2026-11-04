namespace Loadpath.Presentation;

/// <summary>
/// Where the overlays sit on the canvas, in canvas pixels (y down): what an inspector sheet covers, how far to pan so
/// the selection shows beside a sheet, and where the floating selection bar goes. Pure geometry; the page measures the
/// views and applies the answers.
/// </summary>
public static class CanvasLayout
{
    /// <summary>Gap between an element and a bar below it: clear of a support symbol.</summary>
    public const double BarDrop = 28;
    /// <summary>Gap between the selection and a bar beside it.</summary>
    public const double BarSide = 24;
    /// <summary>The bar keeps this far from the top of the canvas (the banners' band).</summary>
    public const double BarTopEdge = 32;
    /// <summary>The bar keeps this far from the canvas' other edges.</summary>
    public const double Gutter = 8;

    /// <summary>
    /// What an open sheet covers: the side sheet's width on the right (Normal), or the bottom sheet's height (Narrow).
    /// Nothing when the inspector is docked or the sheet is closed.
    /// </summary>
    public static (double Right, double Bottom) Covered(bool docked, bool sheetOpen, bool narrow, double sideSheetWidth, double bottomSheetHeight) =>
        docked || !sheetOpen ? (0, 0) : narrow ? (0, bottomSheetHeight) : (sideSheetWidth, 0);

    /// <summary>The part of a <paramref name="width"/> x <paramref name="height"/> canvas that no sheet covers.</summary>
    public static Bounds Uncovered(double width, double height, (double Right, double Bottom) covered) =>
        new(Vec2.Zero, new Vec2(Math.Max(0, width - covered.Right), Math.Max(0, height - covered.Bottom)));

    /// <summary>
    /// The pan that brings <paramref name="selection"/> inside <paramref name="target"/>, moving the view as little as
    /// possible: zero on an axis where it is inside already; centred on an axis where it does not fit.
    /// </summary>
    public static Vec2 RevealShift(Bounds selection, Bounds target)
    {
        static double Shift(double min, double max, double lo, double hi) =>
            max - min > hi - lo ? lo + (hi - lo) / 2 - (min + max) / 2 : min < lo ? lo - min : max > hi ? hi - max : 0;
        return new Vec2(Shift(selection.Min.X, selection.Max.X, target.Min.X, target.Max.X), Shift(selection.Min.Y, selection.Max.Y, target.Min.Y, target.Max.Y));
    }

    /// <summary>
    /// The floating bar's top-left corner: up and to the right of the selection, clear of its load arrow; it flips left
    /// when it would leave the canvas, and drops below the selection near the top edge.
    /// </summary>
    /// <param name="selection">The selected nodes (and selected members' ends) on screen.</param>
    /// <param name="bar">The bar's measured size.</param>
    /// <param name="area">The part of the canvas no sheet covers.</param>
    public static Vec2 FloatingBar(Bounds selection, Vec2 bar, Bounds area)
    {
        var w = bar.X;
        var x = selection.Max.X + BarSide;
        if (x + w > area.Max.X - Gutter) x = selection.Min.X - BarSide - w;
        // Keep 32 px off the left edge when there is room; a phone-wide bar may use the 8 px gutter instead.
        var minX = Math.Min(BarTopEdge, Math.Max(Gutter, area.Max.X - w - Gutter));
        x = Math.Clamp(x, minX, Math.Max(minX, area.Max.X - w - Gutter));
        var y = selection.Min.Y - 80;
        if (y < BarTopEdge) y = selection.Max.Y + BarDrop;
        return new Vec2(x, y);
    }
}
