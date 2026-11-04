using Loadpath.Controls;
using Loadpath.Presentation;
using Loadpath.Workspace;

namespace Loadpath.Views;

/// <summary>
/// The overlays that follow the viewport rather than the layout: the floating selection bar (its contents are bound to
/// the inspector state; its position comes from CanvasLayout.FloatingBar), the canvas inset an open bottom sheet takes,
/// and the pan that keeps the selection in view beside an open sheet. It listens to the engine and applies what
/// CanvasLayout computes.
/// </summary>
public sealed class CanvasOverlays
{
    private readonly EditorEngine _engine;
    private readonly FrameworkElement _host;
    private readonly WorkspaceView _canvas;
    private readonly FrameworkElement _bar;
    private bool _barShown;
    private Vec2 _barAt;
    private bool _narrow;

    /// <param name="host">The canvas host: the area the overlays share with the canvas.</param>
    /// <param name="bar">The floating selection bar.</param>
    public CanvasOverlays(EditorEngine engine, FrameworkElement host, WorkspaceView canvas, FrameworkElement bar)
    {
        _engine = engine;
        _host = host;
        _canvas = canvas;
        _bar = bar;
        engine.SelectionChanged += (_, _) => UpdateBar(glide: true);
        engine.ViewportChanged += (_, _) => UpdateBar();
        engine.AnalysisChanged += (_, _) => UpdateBar();
        engine.Interaction.ToolChanged += (_, _) => UpdateBar();
        engine.OptionsChanged += (_, name) =>
        {
            if (name == nameof(ViewOptions.InspectorSheetOpen) && engine.Options.InspectorSheetOpen) bar.DispatcherQueue.TryEnqueue(RevealSelection);
            if (name is nameof(ViewOptions.InspectorSheetOpen) or nameof(ViewOptions.InspectorDocked)) { UpdateCoveredCanvas(); UpdateBar(); }
        };
        // The bar's label and buttons are bound, so its width settles after the binding; re-anchor when it does.
        bar.SizeChanged += (_, _) => UpdateBar();
    }

    /// <summary>The layout changed (the page's responsive probe): a phone's sheet covers the bottom, a wider one the right.</summary>
    public void OnLayoutChanged(bool narrow)
    {
        _narrow = narrow;
        UpdateCoveredCanvas();
        // Resized into a sheet layout with the sheet open: the sheet now covers part of the drawing.
        if (_engine.Options.InspectorSheetOpen && !_engine.Options.InspectorDocked) _bar.DispatcherQueue.TryEnqueue(RevealSelection);
    }

    /// <summary>What an open sheet covers: its size is a layout token; the layout says which edge it covers.</summary>
    private (double Right, double Bottom) Covered()
    {
        static double Token(string key) => Application.Current.Resources.TryGetValue(key, out var v) && v is double d ? d : 0;
        return CanvasLayout.Covered(_engine.Options.InspectorDocked, _engine.Options.InspectorSheetOpen, _narrow, Token("InspectorWidth"), Token("BottomSheetHeight"));
    }

    private Bounds Uncovered() => CanvasLayout.Uncovered(_host.ActualWidth, _host.ActualHeight, Covered());

    /// <summary>
    /// A phone's bottom sheet leaves the full width, so a fitted structure re-fits above it. Beside a side sheet the
    /// view stays put (refitting into what is left would draw it postage-stamp small); the selection is panned clear instead.
    /// </summary>
    private void UpdateCoveredCanvas() => _canvas.CoveredInsets = new Thickness(0, 0, 0, Covered().Bottom);

    /// <summary>When a sheet opens over the selection, pan so the selection sits just inside the part still showing (clear of the floating rail).</summary>
    private void RevealSelection()
    {
        if (_engine.Options.InspectorDocked || !_engine.Options.InspectorSheetOpen) return;
        var selection = _engine.SelectionOnScreen();
        if (selection.IsEmpty) return;
        var free = Uncovered();
        var fit = _canvas.FitInsets;
        var target = new Bounds(new Vec2(free.Min.X + (_canvas.RailOverlay ? fit.Left : 0), free.Min.Y + fit.Top / 2), new Vec2(free.Max.X - 24, free.Max.Y - 24));
        var shift = CanvasLayout.RevealShift(selection, target);
        if (Math.Abs(shift.X) < 1 && Math.Abs(shift.Y) < 1) return;
        _engine.Interaction.NotifyNavigated();
        _engine.Viewport.PanBy(shift);
    }

    /// <param name="glide">True for a selection change: the bar travels to the new anchor. Viewport moves follow at once.</param>
    private void UpdateBar(bool glide = false)
    {
        var i = _engine.Interaction;
        var selection = _engine.Selection.IsEmpty || i.ActiveTool != ToolKind.Select || i.IsBusy ? Bounds.Empty : _engine.SelectionOnScreen();
        if (selection.IsEmpty) { HideBar(); return; }
        // xaml-lint: allow responsive - "has the bar measured yet", not a layout breakpoint
        var size = _bar.ActualWidth > 0 ? new Vec2(_bar.ActualWidth, _bar.ActualHeight) : new Vec2(150, 32);
        var at = CanvasLayout.FloatingBar(selection, _engine.DrawingOnScreen(), size, Uncovered());
        _bar.Margin = new Thickness(at.X, at.Y, 0, 0);
        if (!_barShown)
        {
            _barShown = true;
            // xaml-lint: allow codebehind - the bar's visibility follows the viewport-projected selection bounds
            _bar.Visibility = Visibility.Visible;
            Motion.Enter(_bar, rise: 4);
        }
        else if (glide)
        {
            Motion.Glide(_bar, _barAt.X - at.X, _barAt.Y - at.Y);
        }
        _barAt = at;
    }

    private void HideBar()
    {
        if (!_barShown) return;
        _barShown = false;
        // xaml-lint: allow codebehind - collapses once the fade-out has played, unless the bar came back meanwhile
        Motion.Exit(_bar, () => { if (!_barShown) _bar.Visibility = Visibility.Collapsed; });
    }
}
