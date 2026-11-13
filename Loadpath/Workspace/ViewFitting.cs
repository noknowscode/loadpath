using Loadpath.Presentation;

namespace Loadpath.Workspace;

/// <summary>Where the sheet frames a structure: the fit insets, and what an open sheet covers (right, bottom).</summary>
internal readonly record struct SheetInsets((double Left, double Top, double Right, double Bottom) Fit, (double Right, double Bottom) Covered);

/// <summary>
/// Keeps the structure framed on the sheet. Fit eases the view to the sheet's frame (<see cref="CanvasLayout.SheetFit"/>),
/// and a fitted view stays fitted through resizes and layout changes until the user moves it. A view the user moved is
/// kept through a resize, unless the resize pushes part of a structure that was all in view off the canvas (a narrower
/// window): then it is fitted again.
/// </summary>
internal sealed class ViewFitting(EditorEngine editor, ViewportAnimation animation, Func<SheetInsets> insets)
{
    private static readonly TimeSpan FitDuration = TimeSpan.FromMilliseconds(280);

    /// <summary>True from a fit until the user pans or zooms.</summary>
    private bool _fitted;

    /// <summary>Ease the view to frame the structure, and keep it framed.</summary>
    public void Fit()
    {
        _fitted = true;
        var (scale, offset) = Target();
        animation.To(scale, offset, FitDuration);
    }

    /// <summary>The user moved the view (pan, zoom, pinch, or a sheet panned clear of the selection): it is theirs now.</summary>
    public void Release()
    {
        _fitted = false;
        animation.Stop();
    }

    public void Resize(double width, double height)
    {
        var viewport = editor.Viewport;
        var bounds = editor.Document.GetBounds();
        var wasInView = viewport.Shows(bounds);
        viewport.Resize(width, height);
        if (editor.Document.Nodes.Count == 0 || !(_fitted || wasInView && !viewport.Shows(bounds))) return;
        animation.Stop();
        _fitted = true;
        Refit();
    }

    /// <summary>The insets or the covered part changed: a fitted view re-frames at once.</summary>
    public void LayoutChanged()
    {
        if (_fitted && editor.Document.Nodes.Count > 0) Refit();
    }

    private void Refit()
    {
        var (scale, offset) = Target();
        editor.Viewport.Set(scale, offset);
    }

    private (double Scale, Vec2 Offset) Target()
    {
        var viewport = editor.Viewport;
        var world = editor.Document.GetBounds();
        if (world.IsEmpty || viewport.ScreenWidth <= 0 || viewport.ScreenHeight <= 0) return viewport.ComputeFit(world);
        var sheet = insets();
        return CanvasLayout.SheetFit(world, viewport.ScreenWidth, viewport.ScreenHeight, sheet.Fit, sheet.Covered);
    }
}
