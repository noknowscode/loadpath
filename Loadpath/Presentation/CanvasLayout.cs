namespace Loadpath.Presentation;

/// <summary>
/// Where the overlays sit on the canvas, in canvas pixels (y down): what an inspector sheet covers, how far to pan so
/// the selection shows beside a sheet, and where the floating selection bar goes. Pure geometry; the page measures the
/// views and applies the answers.
/// </summary>
public static class CanvasLayout
{
    /// <summary>Gap between the bar's bottom and the element it sits above: clear of a load arrow drawn above a node.</summary>
    public const double BarLift = 48;
    /// <summary>Gap between an element and a bar below it: clear of a support symbol.</summary>
    public const double BarDrop = 28;
    /// <summary>Gap between the selection and a bar beside it.</summary>
    public const double BarSide = 24;
    /// <summary>The bar keeps this far from the top of the canvas (the banners' band).</summary>
    public const double BarTopEdge = 32;
    /// <summary>The bar keeps this far from the canvas' other edges, and from anything drawn.</summary>
    public const double Gutter = 8;
    /// <summary>Gap between the top of the drawing and a bar above it: clear of the force labels over a top chord.</summary>
    public const double Clearance = 28;

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
    /// The scale and offset that frame <paramref name="world"/> on a <paramref name="width"/> x <paramref name="height"/>
    /// canvas: inside <paramref name="fit"/> (clear of the floating tool rail on the left, the banners on top and the band
    /// that holds the dimension chain and the legend at the bottom) and out from under an open sheet. Under a bottom sheet
    /// the dimension band is hidden anyway, so the structure keeps as far above the sheet as it is below the title.
    /// </summary>
    public static (double Scale, Vec2 Offset) SheetFit(Bounds world, double width, double height,
        (double Left, double Top, double Right, double Bottom) fit, (double Right, double Bottom) covered)
    {
        double left = fit.Left, top = fit.Top, right = fit.Right + covered.Right;
        var bottom = covered.Bottom > 0 ? covered.Bottom + fit.Top : fit.Bottom;
        var areaW = Math.Max(120, width - left - right);
        var areaH = Math.Max(120, height - top - bottom);
        var scale = Math.Clamp(Math.Min(areaW * 0.9 / Math.Max(world.Width, 1.0), areaH * 0.9 / Math.Max(world.Height, 1.0)),
            Core.Viewport.Viewport.MinScale, Core.Viewport.Viewport.MaxScale);
        var c = world.Center;
        return (scale, new Vec2(left + areaW / 2 - c.X * scale, top + areaH / 2 + c.Y * scale));
    }

    /// <summary>
    /// The floating bar's top-left corner. It sits up and to the right of the selection (left when the right runs out
    /// of canvas), above the selection, and never over the drawing when the canvas has room for it: a bar above a
    /// bottom-chord node would cover the truss and its load arrows (on a phone its 44 px targets make it as wide as the
    /// drawing), so it goes above the whole drawing instead, or below it when the top of the canvas has no room. Only a
    /// drawing that fills the canvas leaves the bar over it.
    /// </summary>
    /// <param name="selection">The selected nodes (and selected members' ends) on screen.</param>
    /// <param name="drawing">What the bar must not cover, as screen segments: members, load arrows with their labels,
    /// supports, and each node as a point (EditorEngine.DrawingOnScreen).</param>
    /// <param name="bar">The bar's measured size.</param>
    /// <param name="area">The part of the canvas no sheet covers.</param>
    public static Vec2 FloatingBar(Bounds selection, IReadOnlyList<(Vec2 A, Vec2 B)> drawing, Vec2 bar, Bounds area)
    {
        var (w, h) = (bar.X, bar.Y);
        var x = selection.Max.X + BarSide;
        if (x + w > area.Max.X - Gutter) x = selection.Min.X - BarSide - w;
        // Keep 32 px off the left edge when there is room; a phone-wide bar may use the 8 px gutter instead.
        var minX = Math.Min(BarTopEdge, Math.Max(Gutter, area.Max.X - w - Gutter));
        x = Math.Clamp(x, minX, Math.Max(minX, area.Max.X - w - Gutter));

        bool Covers(double y)
        {
            var box = new Bounds(new Vec2(x - Gutter, y - Gutter), new Vec2(x + w + Gutter, y + h + Gutter));
            foreach (var (a, b) in drawing)
            {
                if (box.IntersectsSegment(a, b)) return true;
            }
            return false;
        }
        bool Fits(double y) => y >= area.Min.Y + BarTopEdge && y + h <= area.Max.Y - Gutter;

        var aboveSelection = selection.Min.Y - BarLift - h;
        if (Fits(aboveSelection) && !Covers(aboveSelection)) return new Vec2(x, aboveSelection);

        // Above the whole drawing (the sheet is empty there; below it are the reactions and the dimension chain), or
        // below it when the top of the canvas has no room.
        if (drawing.Count > 0)
        {
            double top = double.PositiveInfinity, bottom = double.NegativeInfinity;
            foreach (var (a, b) in drawing)
            {
                top = Math.Min(top, Math.Min(a.Y, b.Y));
                bottom = Math.Max(bottom, Math.Max(a.Y, b.Y));
            }
            var aboveDrawing = top - Clearance - h;
            if (Fits(aboveDrawing) && !Covers(aboveDrawing)) return new Vec2(x, aboveDrawing);
            var belowDrawing = bottom + BarDrop;
            if (Fits(belowDrawing) && !Covers(belowDrawing)) return new Vec2(x, belowDrawing);
        }

        // No clear room: above the selection, or below it near the top edge.
        return new Vec2(x, aboveSelection >= area.Min.Y + BarTopEdge ? aboveSelection : selection.Max.Y + BarDrop);
    }
}
