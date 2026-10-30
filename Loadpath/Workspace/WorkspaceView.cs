using Loadpath.Controls;
using Loadpath.Core.Geometry;
using Loadpath.Core.Input;
using Loadpath.Core.Viewport;
using Loadpath.Presentation;
using Loadpath.Workspace.Tools;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Loadpath.Workspace;

/// <summary>A context menu request from the canvas. Touch requests get finger-sized menu rows.</summary>
public sealed record ContextMenuRequest(Point Position, HitResult Hit, bool Touch);

/// <summary>
/// The canvas. Owns rendering (through WorkspaceRenderer), pointer input (forwarded to WorkspaceInteraction),
/// the viewport size, and the small viewport animation timer.
/// </summary>
public sealed partial class WorkspaceView : SKCanvasElement
{
    public static readonly DependencyProperty EditorProperty = DependencyProperty.Register(
        nameof(Editor), typeof(EditorEngine), typeof(WorkspaceView), new PropertyMetadata(null, (d, e) => ((WorkspaceView)d).Attach(e.OldValue as EditorEngine, e.NewValue as EditorEngine)));

    public static readonly DependencyProperty FitInsetsProperty = DependencyProperty.Register(
        nameof(FitInsets), typeof(Thickness), typeof(WorkspaceView), new PropertyMetadata(default(Thickness), (d, _) => ((WorkspaceView)d).OnLayoutTokensChanged()));

    public static readonly DependencyProperty RailOverlayProperty = DependencyProperty.Register(
        nameof(RailOverlay), typeof(bool), typeof(WorkspaceView), new PropertyMetadata(true, (d, _) => ((WorkspaceView)d).OnLayoutTokensChanged()));

    /// <summary>
    /// Where Fit frames the structure: clear of the floating tool rail (left), the banners (top), the height dimension
    /// (right) and the band that holds the dimension chain and the legend (bottom). Set per layout from Layout.xaml.
    /// </summary>
    public Thickness FitInsets { get => (Thickness)GetValue(FitInsetsProperty); set => SetValue(FitInsetsProperty, value); }

    /// <summary>True while the tool rail floats over the sheet's top-left corner, so labels steer around it.</summary>
    public bool RailOverlay { get => (bool)GetValue(RailOverlayProperty); set => SetValue(RailOverlayProperty, value); }

    private void OnLayoutTokensChanged()
    {
        if (_editor is not null && _fitted && _editor.Document.Nodes.Count > 0)
        {
            var target = SheetFit(_editor.Document.GetBounds());
            _editor.Viewport.Set(target.Scale, target.Offset);
        }
        MarkDirty();
    }

    private readonly WorkspaceRenderer _renderer = new();
    private volatile RenderSnapshot _snapshot = RenderSnapshot.Empty;
    private EditorEngine? _editor;
    private DispatcherTimer? _animTimer;
    private (double Scale, Vec2 Offset) _animFrom;
    private (double Scale, Vec2 Offset) _animTo;
    private DateTime _animStart;
    private TimeSpan _animDuration;
    private bool _snapshotDirty = true;
    private bool _fitted;
    private DateTime _lastTap = DateTime.MinValue;
    private Vec2 _lastTapPos;

    public WorkspaceView()
    {
        IsTabStop = false;
        // Uno 7's SKCanvasElement is a Grid, and a Grid without a background is not hit-testable: presses fell through
        // to the page, so the canvas took no input and the press pulled keyboard focus off the key sink.
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        SizeChanged += (_, e) =>
        {
            _editor?.Viewport.Resize(e.NewSize.Width, e.NewSize.Height);
            // A fitted view stays fitted through window resizes until the user pans or zooms.
            if (_editor is not null && _fitted && _editor.Document.Nodes.Count > 0)
            {
                _animTimer?.Stop();
                var target = SheetFit(_editor.Document.GetBounds());
                _editor.Viewport.Set(target.Scale, target.Offset);
            }
            _editor?.Interaction.RefreshZoomText();
            MarkDirty();
        };
        Unloaded += (_, _) => { _animTimer?.Stop(); };
        // The canvas palette follows the theme: read once now (the theme the page starts in), then on every change.
        _renderer.SetPalette(SkiaPalette.FromResources(ThemeBrushes.Theme));
        Loaded += (_, _) => ApplyThemePalette();
        ActualThemeChanged += (_, _) => ApplyThemePalette();
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
        PointerCanceled += OnPointerLost;
        PointerCaptureLost += OnPointerLost;
        PointerExited += (_, _) => _editor?.Interaction.PointerExited();
#if !ANDROID && !IOS
        // Mouse-wheel zoom. Android and iOS report no wheel delta (Uno0001); zoom there is Ctrl+= / Ctrl+- and Fit.
        PointerWheelChanged += OnPointerWheelChanged;
#endif
        _ = LoadTypefacesAsync();
    }

    private void ApplyThemePalette()
    {
        if (ActualTheme == ElementTheme.Default) return;
        _renderer.SetPalette(SkiaPalette.FromResources(ActualTheme));
        Invalidate();
    }

    /// <summary>The bundled canvas fonts load from the app package; the first frames use the fallback faces.</summary>
    private async Task LoadTypefacesAsync()
    {
        try
        {
            _renderer.SetTypefaces(await CanvasTypefaces.LoadAsync());
            Invalidate();
        }
        catch (Exception)
        {
            // Cosmetic: the canvas keeps its fallback faces.
        }
    }

    public EditorEngine? Editor
    {
        get => (EditorEngine?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>Raised on right-click or a long press, with the screen point, what was hit, and whether a finger asked.</summary>
    public event EventHandler<ContextMenuRequest>? ContextMenuRequested;

    private void Attach(EditorEngine? old, EditorEngine? editor)
    {
        if (old is not null)
        {
            old.RenderRequested -= OnRenderRequested;
            old.ViewportChanged -= OnRenderRequested;
            old.FitRequested -= OnFitRequested;
            old.Interaction.CursorChanged -= OnCursorChanged;
            old.Interaction.UserNavigated -= (_, _) => _fitted = false;
        }
        _editor = editor;
        if (editor is null) return;
        editor.RenderRequested += OnRenderRequested;
        editor.ViewportChanged += OnRenderRequested;
        editor.FitRequested += OnFitRequested;
        editor.Interaction.CursorChanged += OnCursorChanged;
        editor.Interaction.UserNavigated += (_, _) => _fitted = false;
        if (ActualWidth > 0) editor.Viewport.Resize(ActualWidth, ActualHeight);
        MarkDirty();
    }

    private void OnRenderRequested(object? sender, EventArgs e)
    {
        _editor?.Interaction.RefreshZoomText();
        MarkDirty();
    }

    private void MarkDirty()
    {
        _snapshotDirty = true;
        Invalidate();
    }

    protected override void RenderOverride(SKCanvas canvas, Size area)
    {
        if (_snapshotDirty && _editor is not null)
        {
            _snapshot = BuildSnapshot(_editor, (float)area.Width, (float)area.Height, RailOverlay);
            _snapshotDirty = false;
        }
        _renderer.Render(canvas, _snapshot);
    }

    // ---- snapshot ----

    private static RenderSnapshot BuildSnapshot(EditorEngine ed, float width, float height, bool railOverlay)
    {
        var doc = ed.Document;
        var vp = ed.Viewport;
        var analysis = ed.Analysis;
        var sel = ed.Selection;
        var hover = ed.Interaction.Hover;
        var options = ed.Options;
        var anySelected = !sel.IsEmpty;
        var dimOthers = sel.MemberCount == 1 && sel.NodeCount == 0;
        var (minor, major) = GridSteps.For(vp.Scale);

        // Deflection exaggeration: max displacement reads as 6% of the structure diagonal, times the user factor.
        var bounds = doc.GetBounds();
        var diag = bounds.IsEmpty ? 1 : Math.Max(1, Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height));
        var exag = analysis.IsSolved && analysis.MaxDisplacementM > 1e-12 ? diag * 0.06 * options.Exaggeration / analysis.MaxDisplacementM : 0;

        var connected = new HashSet<int>();
        if (dimOthers && doc.FindMember(sel.MemberIds.First()) is { } sm) { connected.Add(sm.StartNodeId); connected.Add(sm.EndNodeId); }
        var detached = analysis.DetachedNodeIds.Count > 0 ? new HashSet<int>(analysis.DetachedNodeIds) : null;

        var nodes = new RenderSnapshot.NodeItem[doc.Nodes.Count];
        var nodeIndex = new Dictionary<int, int>(doc.Nodes.Count);
        for (var i = 0; i < doc.Nodes.Count; i++)
        {
            var n = doc.Nodes[i];
            nodeIndex[n.Id] = i;
            var s = vp.ToScreen(n.Position);
            var r = analysis.ForNode(n.Id);
            var disp = r?.Displacement ?? Vec2.Zero;
            var deflected = vp.ToScreen(n.Position + disp * exag);
            var tail = HitTester.LoadHandleScreen(n, vp);
            var isSelected = sel.Contains(n.Ref);
            var dim = (dimOthers && !connected.Contains(n.Id)) || (detached?.Contains(n.Id) ?? false);
            nodes[i] = new RenderSnapshot.NodeItem(n.Id, P(s), P(deflected), n.Support, n.Load, P(tail), r?.Reaction ?? Vec2.Zero, isSelected, hover == n.Ref, dim, n.Position);
        }

        // Part names for the annotation layer: classified from world geometry, so they do not change with zoom.
        var geometry = new (Vec2 A, Vec2 B)[doc.Members.Count];
        for (var i = 0; i < doc.Members.Count; i++) geometry[i] = (doc.Nodes[nodeIndex[doc.Members[i].StartNodeId]].Position, doc.Nodes[nodeIndex[doc.Members[i].EndNodeId]].Position);
        var parts = PartClassifier.Classify(geometry);
        var supports = doc.Nodes.Where(n => n.HasSupport).Select(n => n.Position).ToList();

        var members = new RenderSnapshot.MemberItem[doc.Members.Count];
        for (var i = 0; i < doc.Members.Count; i++)
        {
            var m = doc.Members[i];
            var a = nodes[nodeIndex[m.StartNodeId]];
            var b = nodes[nodeIndex[m.EndNodeId]];
            var r = analysis.For(m.Id);
            var isSelected = sel.Contains(m.Ref);
            var dim = (dimOthers && !isSelected) || (detached?.Contains(m.StartNodeId) ?? false);
            members[i] = new RenderSnapshot.MemberItem(m.Id, a.Screen, b.Screen, a.Deflected, b.Deflected,
                r?.AxialForceKn ?? 0, r?.Utilization ?? 0, r?.IsOverstressed ?? false, r?.BucklingGoverns ?? false, isSelected, hover == m.Ref, dim, parts[i]);
        }

        return new RenderSnapshot
        {
            Nodes = nodes,
            Members = members,
            MaxAbsForceKn = analysis.MaxAbsForceKn,
            Status = analysis.Status,
            Scale = vp.Scale,
            Origin = P(vp.ToScreen(Vec2.Zero)),
            Mode = options.DisplayMode,
            ShowDeflection = options.IsVisible(NoiseLayer.Deflection),
            ShowLabels = options.IsVisible(NoiseLayer.MemberForces),
            ShowReactions = options.IsVisible(NoiseLayer.Reactions),
            ShowGrid = options.IsVisible(NoiseLayer.Grid),
            ShowDimensions = options.IsVisible(NoiseLayer.Dimensions),
            ShowSupports = options.IsVisible(NoiseLayer.Supports),
            ShowLoads = options.IsVisible(NoiseLayer.Loads),
            ShowOverCapacity = options.IsVisible(NoiseLayer.OverCapacity),
            ShowPartNames = options.IsVisible(NoiseLayer.PartNames),
            ShowLegend = options.IsVisible(NoiseLayer.Legend),
            StructureKind = PartClassifier.Describe(geometry, parts, supports),
            GridMinor = minor,
            GridMajor = major,
            Overlay = ed.Interaction.Overlay,
            Width = width,
            Height = height,
            AnySelected = anySelected,
            DocumentName = doc.Name,
            Exaggeration = options.Exaggeration,
            Center = bounds.IsEmpty ? new SKPoint(width / 2, height / 2) : P(vp.ToScreen(bounds.Center)),
            RailOverlay = railOverlay,
        };
    }

    private static SKPoint P(Vec2 v) => new((float)v.X, (float)v.Y);

    // ---- pointer ----

    private PointerState State(PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        return State(e, new Vec2(pt.Position.X, pt.Position.Y), pt.Properties.IsLeftButtonPressed, pt.Properties.IsMiddleButtonPressed, pt.Properties.IsRightButtonPressed);
    }

    private PointerState State(PointerRoutedEventArgs e, Vec2 screen, bool left, bool middle = false, bool right = false)
    {
        var mods = e.KeyModifiers;
        var options = _editor?.Options;
        return new PointerState(
            screen,
            _editor?.Viewport.ToWorld(screen) ?? Vec2.Zero,
            mods.HasFlag(Windows.System.VirtualKeyModifiers.Shift),
            mods.HasFlag(Windows.System.VirtualKeyModifiers.Menu),
            mods.HasFlag(Windows.System.VirtualKeyModifiers.Control),
            left, middle, right,
            Multi: options?.MultiSelect ?? false,
            Free: options?.FreePlacement ?? false,
            IsTouch: IsTouch(e));
    }

    /// <summary>A finger. DEBUG builds can route the mouse through the touch path (LOADPATH_TOUCH=1) for headless checks.</summary>
    private static bool IsTouch(PointerRoutedEventArgs e) =>
        e.Pointer.PointerDeviceType == PointerDeviceType.Touch || EmulateTouch;

#if DEBUG
    private static readonly bool EmulateTouch = Environment.GetEnvironmentVariable("LOADPATH_TOUCH") == "1";
#else
    private const bool EmulateTouch = false;
#endif

    private PointerState _lastState;

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_editor is null) return;
        FocusRequested?.Invoke(this, EventArgs.Empty);
        if (IsTouch(e))
        {
            CapturePointer(e.Pointer);
            OnTouch(_touch.Down(e.Pointer.PointerId, Position(e), Environment.TickCount64), e);
            e.Handled = true;
            return;
        }
        var s = State(e);
        _lastState = s;
#if DEBUG
        if (Environment.GetEnvironmentVariable("LOADPATH_TRACE") is { } tr)
        {
            var pp = e.GetCurrentPoint(this).Properties;
            File.AppendAllText(tr, $"press kind={pp.PointerUpdateKind} l={pp.IsLeftButtonPressed} m={pp.IsMiddleButtonPressed} r={pp.IsRightButtonPressed} x1={pp.IsXButton1Pressed} x2={pp.IsXButton2Pressed}\n");
        }
#endif
        if (s.IsRight)
        {
            OpenContextMenu(s, touch: false);
            e.Handled = true;
            return;
        }
        CapturePointer(e.Pointer);
        if (s.IsMiddle || _editor.Interaction.ActiveTool == ToolKind.Pan) _fitted = false;
        Press(s);
        e.Handled = true;
    }

    /// <summary>A press for the tools, with double-tap detection (two presses within 350 ms and 6 px).</summary>
    private void Press(PointerState s)
    {
        _lastState = s;
        var now = DateTime.UtcNow;
        if ((now - _lastTap).TotalMilliseconds < 350 && (s.Screen - _lastTapPos).Length < 6)
        {
            _editor!.Interaction.DoubleTap(s);
            _lastTap = DateTime.MinValue;
        }
        else
        {
            _lastTap = now;
            _lastTapPos = s.Screen;
            _editor!.Interaction.PointerPressed(s);
        }
    }

    private void OpenContextMenu(PointerState s, bool touch)
    {
        _editor!.Interaction.CancelTool();
        _editor.Interaction.Radii = HitRadii.For(touch);
        var hit = _editor.Interaction.Hit(s.Screen);
        if (hit.Ref is { } r && !_editor.Selection.Contains(r)) _editor.Selection.Set(r);
        ContextMenuRequested?.Invoke(this, new ContextMenuRequest(new Point(s.Screen.X, s.Screen.Y), hit, touch));
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_editor is null) return;
        if (IsTouch(e))
        {
            OnTouch(_touch.Move(e.Pointer.PointerId, Position(e)), e);
            e.Handled = true;
            return;
        }
        var s = State(e);
        _lastState = s;
        _editor.Interaction.PointerMoved(s);
        e.Handled = true;
    }

    private void OnPointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_editor is null) return;
        if (IsTouch(e))
        {
            OnTouch(_touch.Up(e.Pointer.PointerId, Position(e)), e);
            ReleasePointerCapture(e.Pointer);
            e.Handled = true;
            return;
        }
        var s = State(e);
        _editor.Interaction.PointerReleased(s);
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerLost(object sender, PointerRoutedEventArgs e)
    {
        if (IsTouch(e))
        {
            OnTouch(_touch.Cancel(e.Pointer.PointerId), e);
            return;
        }
        _editor?.Interaction.PointerReleased(_lastState with { IsLeft = false, IsMiddle = false });
    }

    // ---- touch: one finger edits like the left button; two fingers pinch-zoom and pan; a resting finger opens the menu ----

    private readonly TouchTracker _touch = new();
    private PinchGesture? _pinch;
    private DispatcherTimer? _longPressTimer;

    private Vec2 Position(PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this).Position;
        return new Vec2(pt.X, pt.Y);
    }

    private void OnTouch(TouchSignal signal, PointerRoutedEventArgs e)
    {
        var interaction = _editor!.Interaction;
        switch (signal)
        {
            case TouchSignal.None:
                break;
            case TouchSignal.BeginDrag:
                Press(State(e, _touch.PressPoint, left: true));
                interaction.PointerMoved(_lastState = State(e, Position(e), left: true));
                break;
            case TouchSignal.Drag:
                interaction.PointerMoved(_lastState = State(e, Position(e), left: true));
                break;
            case TouchSignal.EndDrag:
                interaction.PointerReleased(State(e, Position(e), left: false));
                break;
            case TouchSignal.Tap:
                var at = State(e, _touch.PressPoint, left: true);
                Press(at);
                interaction.PointerReleased(at with { IsLeft = false });
                break;
            case TouchSignal.PinchStart:
                // A held press never reached a tool; a drag in progress is cancelled (undone) before the view moves.
                if (interaction.IsBusy) interaction.CancelTool();
                _fitted = false;
                var (a, b) = _touch.PinchPoints;
                _pinch = new PinchGesture(_editor.Viewport, a, b);
                break;
            case TouchSignal.Pinch:
                if (_pinch is not null)
                {
                    var (p1, p2) = _touch.PinchPoints;
                    _animTimer?.Stop();
                    _pinch.Apply(_editor.Viewport, p1, p2);
                }
                break;
            case TouchSignal.LongPress:
                OpenContextMenu(State(e, _touch.PressPoint, left: false), touch: true);
                break;
        }
        if (!_touch.IsPinching) _pinch = null;
        UpdateLongPressTimer();
    }

    /// <summary>Runs only while a press is held back; the long press fires from here, with no pointer event to carry it.</summary>
    private void UpdateLongPressTimer()
    {
        if (!_touch.IsPending)
        {
            _longPressTimer?.Stop();
            return;
        }
        if (_longPressTimer is null)
        {
            _longPressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(60) };
            _longPressTimer.Tick += (_, _) =>
            {
                if (_editor is null || _touch.Tick(Environment.TickCount64) != TouchSignal.LongPress)
                {
                    if (!_touch.IsPending) _longPressTimer!.Stop();
                    return;
                }
                _longPressTimer!.Stop();
                var p = _touch.PressPoint;
                OpenContextMenu(new PointerState(p, _editor.Viewport.ToWorld(p), false, false, false, false, false, false,
                    _editor.Options.MultiSelect, _editor.Options.FreePlacement, IsTouch: true), touch: true);
            };
        }
        if (!_longPressTimer.IsEnabled) _longPressTimer.Start();
    }

#if !ANDROID && !IOS
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_editor is null) return;
        var pt = e.GetCurrentPoint(this);
        var delta = pt.Properties.MouseWheelDelta;
#if DEBUG
        if (Environment.GetEnvironmentVariable("LOADPATH_TRACE") is { } trace) File.AppendAllText(trace, $"wheel {delta} h={pt.Properties.IsHorizontalMouseWheel}\n");
#endif
        if (delta == 0) return;
        var mods = e.KeyModifiers;
        var screen = new Vec2(pt.Position.X, pt.Position.Y);
        _fitted = false;
        if (mods.HasFlag(Windows.System.VirtualKeyModifiers.Shift) || pt.Properties.IsHorizontalMouseWheel)
        {
            _editor.Viewport.PanBy(new Vec2(delta > 0 ? 60 : -60, 0));
        }
        else
        {
            var steps = Math.Sign(delta) * Math.Max(1, Math.Abs(delta) / 120.0);
            AnimateZoom(screen, _editor.Viewport.Scale * Math.Pow(1.1, steps));
        }
        e.Handled = true;
    }
#endif

    /// <summary>The page focuses its key sink when the canvas is pressed, so shortcuts work after a click.</summary>
    public event EventHandler? FocusRequested;

    private void OnCursorChanged(object? sender, CursorKind cursor)
    {
#if __DESKTOP__ || __WASM__ || WINDOWS
        // Pointer heads only. Android and iOS are touch-first and draw no cursor, so there is nothing to set.
        var shape = cursor switch
        {
            CursorKind.Hand => InputSystemCursorShape.Hand,
            CursorKind.Grab => InputSystemCursorShape.Hand,
            CursorKind.Grabbing => InputSystemCursorShape.SizeAll,
            CursorKind.Cross => InputSystemCursorShape.Cross,
            CursorKind.Move => InputSystemCursorShape.SizeAll,
            _ => InputSystemCursorShape.Arrow,
        };
        try { ProtectedCursor = InputSystemCursor.Create(shape); } catch { /* cursor shapes are cosmetic */ }
#endif
    }

    // ---- viewport animation (timer only while animating; no Rendering pump) ----

    private void OnFitRequested(object? sender, EventArgs e)
    {
        if (_editor is null) return;
        _fitted = true;
        var bounds = _editor.Document.GetBounds();
        var target = SheetFit(bounds);
        AnimateViewport(target.Scale, target.Offset, TimeSpan.FromMilliseconds(280));
    }

    /// <summary>
    /// Fit into the drawing area of the sheet: inside the frame, clear of the floating tool palette on the left,
    /// and above the band that holds dimension lines and the figure legend.
    /// </summary>
    private (double Scale, Vec2 Offset) SheetFit(Bounds world)
    {
        var vp = _editor!.Viewport;
        if (world.IsEmpty || vp.ScreenWidth <= 0 || vp.ScreenHeight <= 0) return vp.ComputeFit(world);
        var insets = FitInsets;
        double left = insets.Left, top = insets.Top, right = insets.Right, bottom = insets.Bottom;
        var areaW = Math.Max(120, vp.ScreenWidth - left - right);
        var areaH = Math.Max(120, vp.ScreenHeight - top - bottom);
        var w = Math.Max(world.Width, 1.0);
        var h = Math.Max(world.Height, 1.0);
        var scale = Math.Clamp(Math.Min(areaW * 0.9 / w, areaH * 0.9 / h), Core.Viewport.Viewport.MinScale, Core.Viewport.Viewport.MaxScale);
        var c = world.Center;
        return (scale, new Vec2(left + areaW / 2 - c.X * scale, top + areaH / 2 + c.Y * scale));
    }

    private void AnimateZoom(Vec2 anchor, double newScale)
    {
        if (_editor is null) return;
        var vp = _editor.Viewport;
        var s = Math.Clamp(newScale, Core.Viewport.Viewport.MinScale, Core.Viewport.Viewport.MaxScale);
        // Compute the target offset that keeps the anchor fixed, then ease to it.
        var worldAnchor = vp.ToWorld(anchor);
        var offset = new Vec2(anchor.X - worldAnchor.X * s, anchor.Y + worldAnchor.Y * s);
        AnimateViewport(s, offset, TimeSpan.FromMilliseconds(120));
    }

    private void AnimateViewport(double scale, Vec2 offset, TimeSpan duration)
    {
        if (_editor is null) return;
        var vp = _editor.Viewport;
        if (!MotionSettings.AnimationsEnabled || duration == TimeSpan.Zero)
        {
            vp.Set(scale, offset);
            return;
        }
        _animFrom = (vp.Scale, vp.Offset);
        _animTo = (scale, offset);
        _animStart = DateTime.UtcNow;
        _animDuration = duration;
        _animTimer ??= CreateTimer();
        if (!_animTimer.IsEnabled) _animTimer.Start();
    }

    private DispatcherTimer CreateTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        t.Tick += (_, _) =>
        {
            if (_editor is null) { t.Stop(); return; }
            var k = Math.Clamp((DateTime.UtcNow - _animStart).TotalMilliseconds / _animDuration.TotalMilliseconds, 0, 1);
            var e = MotionSettings.EaseInOut(k);
            // Interpolate scale geometrically so zoom feels linear.
            var scale = _animFrom.Scale * Math.Pow(_animTo.Scale / _animFrom.Scale, e);
            var offset = Vec2.Lerp(_animFrom.Offset, _animTo.Offset, e);
            _editor.Viewport.Set(scale, offset);
            if (k >= 1) t.Stop();
        };
        return t;
    }
}
