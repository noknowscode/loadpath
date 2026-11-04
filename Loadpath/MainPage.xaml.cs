using Loadpath.Controls;
using Loadpath.Presentation;
using Loadpath.Services;
using Loadpath.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml.Input;
using Windows.System;

namespace Loadpath;

/// <summary>
/// The single page. It composes the shell from the app's services, routes keyboard shortcuts and tells the engine
/// which layout is active. The overlays that follow the viewport (CanvasOverlays), the canvas menu, the section drag,
/// toasts, the theme, settings, the autosave and the start sequence live in their own classes (App, AppServices).
/// </summary>
public sealed partial class MainPage : Page
{
    private readonly ThemeSwitcher _theme;
    private readonly EditorStartup _startup;
    private readonly SectionDragController _sectionDrag;
    private readonly CanvasOverlays _overlays;
    private readonly ILogger _log;

    public MainPage()
    {
        var services = App.Services;
        Engine = services.GetRequiredService<EditorEngine>();
        _theme = services.GetRequiredService<ThemeSwitcher>();
        _startup = services.GetRequiredService<EditorStartup>();
        _log = services.GetRequiredService<ILogger<MainPage>>();
        // Before the views are built, so the canvas and the converter brushes start in the saved theme.
        _startup.BeforeViews();
        _theme.PrepareBrushes();
        InitializeComponent();

        // MVUX: the generated view model is the binding surface; the engine stays reachable for pointer-level code.
        var viewModel = services.GetRequiredService<EditorViewModel>();
        Model = viewModel.Model;
        DataContext = viewModel;
        Workspace.Log = services.GetRequiredService<ILogger<global::Loadpath.Workspace.WorkspaceView>>();
        Workspace.Ui = services.GetRequiredService<IUiThread>();
        Workspace.Editor = Engine;
        Outline.Model = Model;
        OutlineTab.Model = Model;
        Palette.Model = Model;
        Toasts.Queue = services.GetRequiredService<ToastQueue>();
        _sectionDrag = new SectionDragController(Engine, Model, Root, WorkspaceHost, Workspace, DragGhost, DragGhostText);
        Inspector.SectionDragStarted += (_, section) => _sectionDrag.Start(section);
        Workspace.ContextMenuRequested += (_, request) => CanvasMenu.Show(Engine, Model, Workspace, request);

        // MVUX runs state callbacks off the UI thread: the palette's entrance and the focus move are posted back to it.
        Model.IsPaletteOpen.ForEach(async (open, ct) => DispatcherQueue.TryEnqueue(() =>
        {
            if (open) Palette.OnOpened();
            else KeySink.Focus(FocusState.Programmatic);
        }));
        _overlays = new CanvasOverlays(Engine, WorkspaceHost, Workspace, FloatingBar);
        Engine.SelectionChanged += (_, _) => EnsureKeyboardTarget();
        Workspace.FocusRequested += (_, _) =>
        {
            // The press may select something else: a number still being typed goes to the element it was typed for.
            if (XamlRoot is { } root) NumberField.CommitFocused(root);
            KeySink.Focus(FocusState.Programmatic);
        };
        Root.AddHandler(KeyDownEvent, new KeyEventHandler(OnRootKeyDown), true);
        Root.AddHandler(KeyUpEvent, new KeyEventHandler(OnRootKeyUp), true);
        if (_log.IsEnabled(LogLevel.Trace))
        {
            Root.AddHandler(PointerPressedEvent, new PointerEventHandler((_, e) => _log.LogTrace("page press src={Source} pid={Pointer}", e.OriginalSource?.GetType().Name, e.Pointer.PointerId)), true);
            Root.GotFocus += (_, e) => _log.LogTrace("focus -> {Element} \"{Name}\"", e.OriginalSource?.GetType().Name,
                e.OriginalSource is DependencyObject d ? Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(d) : "");
        }
        Loaded += OnLoaded;
        LayoutProbe.RegisterPropertyChangedCallback(TagProperty, (_, _) => OnLayoutChanged());
        OnLayoutChanged();
    }

    public EditorEngine Engine { get; }
    public EditorModel Model { get; }

    // xaml-lint: allow codebehind - startup: the theme service needs loaded content, then settings and the autosave (no XAML surface)
    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        KeySink.Focus(FocusState.Programmatic);
        await GuardAsync("Theme", () => _theme.StartAsync(this));
        await GuardAsync("Start", _startup.StartAsync);
    }

    /// <summary>
    /// The responsive layout (LayoutProbe.Tag, from the one DefaultResponsiveLayout) decides whether the inspector is a
    /// docked column or a sheet; Ctrl+\ and the inspector button act on whichever it is. At Widest the outline docks
    /// beside the canvas, so the inspector host goes back to its inspector tab.
    /// </summary>
    private void OnLayoutChanged()
    {
        var layout = LayoutProbe.Tag as string ?? "Widest";
        Model.SetInspectorDocked(layout is "Wide" or "Widest");
        _overlays.OnLayoutChanged(narrow: layout == "Narrow");
        if (layout == "Widest") _ = GuardAsync("Inspector tab", () => Model.SelectTabAsync(outline: false).AsTask());
        _log.LogDebug("layout={Layout} width={Width:0}", layout, XamlRoot?.Size.Width ?? 0);
#if DEBUG
        Console.Error.WriteLine($"VERIFY layout={layout} width={XamlRoot?.Size.Width ?? 0:0}");
#endif
    }

    /// <summary>
    /// When the focused element leaves the tree (an inspector panel collapses, the palette closes), keyboard events
    /// have no target. Focus returns to the key sink so shortcuts keep working.
    /// </summary>
    private void EnsureKeyboardTarget()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            if (XamlRoot is not { } root) return;
            var focused = FocusManager.GetFocusedElement(root);
            // xaml-lint: allow codebehind - reads Visibility to find a detached focus target; sets nothing
            var detached = focused is FrameworkElement fe && (fe.XamlRoot is null || !fe.IsLoaded || fe.Visibility == Visibility.Collapsed);
            if (focused is null || detached) KeySink.Focus(FocusState.Programmatic);
        });
    }

    private static bool IsTextInputFocused(XamlRoot root) => FocusManager.GetFocusedElement(root) is TextBox or PasswordBox or RichEditBox;

    // xaml-lint: allow codebehind - editor-wide shortcut routing (handledEventsToo), tools get first refusal
    private async void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        var ctrl = KeyboardState.IsDown(VirtualKey.Control);
        var shift = KeyboardState.IsDown(VirtualKey.Shift);
        var alt = KeyboardState.IsDown(VirtualKey.Menu);
        var key = e.Key;
        _log.LogTrace("key {Key} ctrl={Ctrl} src={Source}", key, ctrl, e.OriginalSource?.GetType().Name);

        if (XamlRoot is { } xr && IsTextInputFocused(xr))
        {
            // The palette shortcut works from anywhere, including a focused number field.
            if (ctrl && key == VirtualKey.K && Palette.Visibility != Visibility.Visible) { e.Handled = true; await GuardAsync("Command palette", () => Model.TogglePalette(default).AsTask()); return; }
            if (key == VirtualKey.Escape && Palette.Visibility != Visibility.Visible) { KeySink.Focus(FocusState.Programmatic); e.Handled = true; }
            return;
        }
        // xaml-lint: allow codebehind - reads the palette's Visibility to route Esc; sets nothing
        if (Palette.Visibility == Visibility.Visible)
        {
            if (key == VirtualKey.Escape) { e.Handled = true; await GuardAsync("Command palette", () => Model.ClosePalette(default).AsTask()); }
            return;
        }

        try
        {
            if (key == VirtualKey.Escape && _sectionDrag.IsDragging) { _sectionDrag.End(drop: false); e.Handled = true; return; }
            // Tools get first refusal (Esc cancels a chain or drag).
            if (!ctrl && !alt && Engine.Interaction.KeyDown(key)) { e.Handled = true; return; }

            var command = Engine.Commands.Resolve(key, ctrl, shift, alt);
            if (command is null) return;
            // Tool switches wait for a gesture to end.
            if (command.Id.StartsWith("tool.") && Engine.Interaction.IsBusy) return;
            if (command.TryExecute()) e.Handled = true;
        }
        catch (Exception ex)
        {
            // An event handler is async void: a throwing shortcut must end as a toast, not take the app down.
            e.Handled = true;
            Model.ReportFailure($"{key}", ex);
        }
    }

    // xaml-lint: allow codebehind - releases held keys (Space pan, Shift axis lock) in the interaction engine
    private void OnRootKeyUp(object sender, KeyRoutedEventArgs e) => Engine.Interaction.KeyUp(e.Key);

    /// <summary>Awaits UI work started from an event handler; a failure is logged and becomes a toast.</summary>
    private async Task GuardAsync(string action, Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            Model.ReportFailure(action, ex);
        }
    }
}
