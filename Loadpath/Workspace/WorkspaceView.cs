using Loadpath.Controls;
using Loadpath.Presentation;
using Loadpath.Workspace.Tools;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml.Input;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;
using Windows.Foundation;

namespace Loadpath.Workspace;

/// <summary>A context menu request from the canvas. Touch requests get finger-sized menu rows.</summary>
public sealed record ContextMenuRequest(Point Position, HitResult Hit, bool Touch);

/// <summary>
/// The canvas. Draws the engine's frame (RenderSnapshotBuilder, WorkspaceRenderer), routes the mouse and the wheel to
/// WorkspaceInteraction and fingers to TouchInput, and sets the cursor. ViewFitting keeps the structure framed.
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

    public static readonly DependencyProperty CoveredInsetsProperty = DependencyProperty.Register(
        nameof(CoveredInsets), typeof(Thickness), typeof(WorkspaceView), new PropertyMetadata(default(Thickness), (d, _) => ((WorkspaceView)d).OnLayoutTokensChanged()));

    /// <summary>
    /// How much of the canvas an open inspector sheet covers. Fit frames the structure in what is left: above a phone's
    /// bottom sheet, with the dimension band (hidden by the sheet anyway) given up.
    /// </summary>
    public Thickness CoveredInsets { get => (Thickness)GetValue(CoveredInsetsProperty); set => SetValue(CoveredInsetsProperty, value); }

    /// <summary>True while the tool rail floats over the sheet's top-left corner, so labels steer around it.</summary>
    public bool RailOverlay { get => (bool)GetValue(RailOverlayProperty); set => SetValue(RailOverlayProperty, value); }

    private void OnLayoutTokensChanged()
    {
        _fitting?.LayoutChanged();
        MarkDirty();
    }

    private readonly WorkspaceRenderer _renderer;
    private volatile RenderSnapshot _snapshot = RenderSnapshot.Empty;
    private EditorEngine? _editor;
    private ViewportAnimation? _animation;
    private ViewFitting? _fitting;
    private TouchInput? _touchInput;
    private bool _snapshotDirty = true;
    private DateTime _lastTap = DateTime.MinValue;
    private Vec2 _lastTapPos;

    public WorkspaceView()
    {
        IsTabStop = false;
        // Uno 7's SKCanvasElement is a Grid, and a Grid without a background is not hit-testable: presses fell through
        // to the page, so the canvas took no input and the press pulled keyboard focus off the key sink.
        Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent);
        SizeChanged += (_, e) => OnSizeChanged(e.NewSize);
        Unloaded += (_, _) => _animation?.Stop();
        // The canvas palette follows the theme: read once now (the theme the page starts in), then on every change.
        _renderer = new WorkspaceRenderer(SkiaPalette.FromResources(ThemeBrushes.Theme));
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
        catch (Exception ex)
        {
            // Cosmetic: the canvas keeps its fallback faces.
            Log.LogWarning(ex, "The canvas fonts did not load; drawing with the fallback faces");
        }
    }

    public EditorEngine? Editor
    {
        get => (EditorEngine?)GetValue(EditorProperty);
        set => SetValue(EditorProperty, value);
    }

    /// <summary>Pointer diagnostics (button states, wheel deltas) at Trace level. Set by the page; silent until then.</summary>
    public ILogger Log { get; set; } = NullLogger.Instance;

    /// <summary>The UI thread's timers, for the long press. Set by the page before <see cref="Editor"/>.</summary>
    public IUiThread? Ui { get; set; }

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
            old.Interaction.UserNavigated -= OnUserNavigated;
        }
        _animation?.Stop();
        (_editor, _animation, _fitting, _touchInput) = (editor, null, null, null);
        if (editor is null) return;
        _animation = new ViewportAnimation(editor.Viewport);
        _fitting = new ViewFitting(editor, _animation, Insets);
        if (Ui is { } ui) _touchInput = new TouchInput(editor, new TouchCanvas(this, editor), ui);
        else Log.LogWarning("No UI thread was set before the editor: touch input is off");
        editor.RenderRequested += OnRenderRequested;
        editor.ViewportChanged += OnRenderRequested;
        editor.FitRequested += OnFitRequested;
        editor.Interaction.CursorChanged += OnCursorChanged;
        editor.Interaction.UserNavigated += OnUserNavigated;
        if (ActualWidth > 0) editor.Viewport.Resize(ActualWidth, ActualHeight);
        MarkDirty();
    }

    /// <summary>The user panned or zoomed: stop keeping the structure fitted through resizes.</summary>
    private void OnUserNavigated(object? sender, EventArgs e) => _fitting?.Release();

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
            _snapshot = RenderSnapshotBuilder.Build(_editor, (float)area.Width, (float)area.Height, RailOverlay);
            _snapshotDirty = false;
        }
        _renderer.Render(canvas, _snapshot);
    }

    // ---- pointer: the mouse here, a finger through TouchInput ----

    private PointerState State(PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this);
        var mods = e.KeyModifiers;
        var options = _editor?.Options;
        var screen = new Vec2(pt.Position.X, pt.Position.Y);
        return new PointerState(
            screen,
            _editor?.Viewport.ToWorld(screen) ?? Vec2.Zero,
            mods.HasFlag(Windows.System.VirtualKeyModifiers.Shift),
            mods.HasFlag(Windows.System.VirtualKeyModifiers.Menu),
            mods.HasFlag(Windows.System.VirtualKeyModifiers.Control),
            pt.Properties.IsLeftButtonPressed, pt.Properties.IsMiddleButtonPressed, pt.Properties.IsRightButtonPressed,
            Multi: options?.MultiSelect ?? false,
            Free: options?.FreePlacement ?? false);
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

    private Vec2 Position(PointerRoutedEventArgs e)
    {
        var pt = e.GetCurrentPoint(this).Position;
        return new Vec2(pt.X, pt.Y);
    }

    private void OnPointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (_editor is null) return;
        FocusRequested?.Invoke(this, EventArgs.Empty);
        if (IsTouch(e))
        {
            CapturePointer(e.Pointer);
            _touchInput?.Down(e.Pointer.PointerId, Position(e), e.KeyModifiers);
            e.Handled = true;
            return;
        }
        var s = State(e);
        _lastState = s;
        if (Log.IsEnabled(LogLevel.Trace))
        {
            var pp = e.GetCurrentPoint(this).Properties;
            Log.LogTrace("press kind={Kind} l={Left} m={Middle} r={Right} x1={X1} x2={X2}", pp.PointerUpdateKind, pp.IsLeftButtonPressed, pp.IsMiddleButtonPressed, pp.IsRightButtonPressed, pp.IsXButton1Pressed, pp.IsXButton2Pressed);
        }
        if (s.IsRight)
        {
            OpenContextMenu(_editor, s);
            e.Handled = true;
            return;
        }
        CapturePointer(e.Pointer);
        if (s.IsMiddle || _editor.Interaction.ActiveTool == ToolKind.Pan) _fitting?.Release();
        Press(_editor, s);
        e.Handled = true;
    }

    /// <summary>A press for the tools, with double-tap detection (two presses within 350 ms and 6 px).</summary>
    private void Press(EditorEngine editor, PointerState s)
    {
        _lastState = s;
        var now = DateTime.UtcNow;
        if ((now - _lastTap).TotalMilliseconds < 350 && (s.Screen - _lastTapPos).Length < 6)
        {
            editor.Interaction.DoubleTap(s);
            _lastTap = DateTime.MinValue;
        }
        else
        {
            _lastTap = now;
            _lastTapPos = s.Screen;
            editor.Interaction.PointerPressed(s);
        }
    }

    private void OpenContextMenu(EditorEngine editor, PointerState s)
    {
        editor.Interaction.CancelTool();
        editor.Interaction.Radii = HitRadii.For(s.IsTouch);
        var hit = editor.Interaction.Hit(s.Screen);
        if (hit.Ref is { } r && !editor.Selection.Contains(r)) editor.Selection.Set(r);
        ContextMenuRequested?.Invoke(this, new ContextMenuRequest(new Point(s.Screen.X, s.Screen.Y), hit, s.IsTouch));
    }

    private void OnPointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_editor is null) return;
        if (IsTouch(e))
        {
            _touchInput?.Move(e.Pointer.PointerId, Position(e), e.KeyModifiers);
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
            _touchInput?.Up(e.Pointer.PointerId, Position(e), e.KeyModifiers);
            ReleasePointerCapture(e.Pointer);
            e.Handled = true;
            return;
        }
        _editor.Interaction.PointerReleased(State(e));
        ReleasePointerCapture(e.Pointer);
        e.Handled = true;
    }

    private void OnPointerLost(object sender, PointerRoutedEventArgs e)
    {
        if (IsTouch(e))
        {
            _touchInput?.Cancel(e.Pointer.PointerId, Position(e), e.KeyModifiers);
            return;
        }
        _editor?.Interaction.PointerReleased(_lastState with { IsLeft = false, IsMiddle = false });
    }

    /// <summary>What a finger drives, through <see cref="TouchInput"/>: the same press, move and release as the mouse.</summary>
    private sealed class TouchCanvas(WorkspaceView view, EditorEngine editor) : ITouchCanvas
    {
        public void Press(PointerState state) => view.Press(editor, state);

        public void Move(PointerState state)
        {
            view._lastState = state;
            editor.Interaction.PointerMoved(state);
        }

        public void Release(PointerState state) => editor.Interaction.PointerReleased(state);

        public void OpenContextMenu(PointerState state) => view.OpenContextMenu(editor, state);

        public void TakeOverView() => view._fitting?.Release();
    }

#if !ANDROID && !IOS
    private void OnPointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (_editor is null) return;
        var pt = e.GetCurrentPoint(this);
        var delta = pt.Properties.MouseWheelDelta;
        Log.LogTrace("wheel {Delta} h={Horizontal}", delta, pt.Properties.IsHorizontalMouseWheel);
        if (delta == 0) return;
        var mods = e.KeyModifiers;
        var screen = new Vec2(pt.Position.X, pt.Position.Y);
        _fitting?.Release();
        if (mods.HasFlag(Windows.System.VirtualKeyModifiers.Shift) || pt.Properties.IsHorizontalMouseWheel)
        {
            _editor.Viewport.PanBy(new Vec2(delta > 0 ? 60 : -60, 0));
        }
        else
        {
            var steps = Math.Sign(delta) * Math.Max(1, Math.Abs(delta) / 120.0);
            _animation?.ZoomAt(screen, _editor.Viewport.Scale * Math.Pow(1.1, steps), TimeSpan.FromMilliseconds(120));
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
        try { ProtectedCursor = InputSystemCursor.Create(shape); }
        catch (Exception ex) { Log.LogDebug(ex, "Cursor {Shape} not set; the shape is cosmetic", shape); }
#endif
    }

    private void OnSizeChanged(Size size)
    {
        _fitting?.Resize(size.Width, size.Height);
        _editor?.Interaction.RefreshZoomText();
        MarkDirty();
    }

    private void OnFitRequested(object? sender, EventArgs e) => _fitting?.Fit();

    private SheetInsets Insets()
    {
        var (fit, covered) = (FitInsets, CoveredInsets);
        return new SheetInsets((fit.Left, fit.Top, fit.Right, fit.Bottom), (covered.Right, covered.Bottom));
    }
}
