using Loadpath.Presentation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Uno.Extensions;
using Uno.Extensions.Hosting;

namespace Loadpath;

/// <summary>
/// The composition root. It builds the host (logging, the theme service and the app's services, AppServices), owns the
/// window (launch size, close guard) and reports failures nothing else caught. The single page resolves its view model
/// and controllers from <see cref="Host"/>.
/// </summary>
public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    public Window? MainWindow { get; private set; }

    /// <summary>The app's services. Built in OnLaunched, before the page is created.</summary>
    public IHost? Host { get; private set; }

    /// <summary>The window's services, for the page (the frame creates it, so nothing can inject them).</summary>
    public static IServiceProvider Services => ((App)Current)._windowScope?.ServiceProvider ?? throw new InvalidOperationException("The host is built in OnLaunched.");

    /// <summary>The one window's scope: the Extensions theme service is scoped to a window.</summary>
    private IServiceScope? _windowScope;

    // The host's Build() is marked RequiresUnreferencedCode for configuration binding (TypeDescriptor converters). The
    // app binds no configuration sections; its services are registered by type with constructor injection, which the
    // generic registrations annotate for the trimmer. The trimmed WebAssembly publish is run in Chromium (README, Validation).
    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode", Justification = "No configuration binding; see the comment above.")]
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
#if __DESKTOP__
        // Size the window at creation; resizing after the fact races the Skia host's surface.
        // Desktop only: WebAssembly, Android and iOS fill the browser or the screen, and WinAppSDK sizes its
        // AppWindow once the content knows the display scale (SizeWindowAtLaunch).
        Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize = new Windows.Foundation.Size(LaunchWidth, LaunchHeight);
#endif
        var builder = this.CreateBuilder(args);
        var window = builder.Window;
        var ui = DispatcherQueue.GetForCurrentThread();
        builder.Configure(host => host
#if DEBUG
            .UseEnvironment(Environments.Development)
#endif
            .UseLogging(configure: (context, logging) => logging
                // Debug builds log the app's own decisions (palette runs, preset parameters, layouts), and with
                // LOADPATH_TRACE=1 every key, press and wheel step too; Release logs what went wrong. The framework stays
                // at warnings either way.
                .SetMinimumLevel(!context.HostingEnvironment.IsDevelopment() ? LogLevel.Warning
                    : Environment.GetEnvironmentVariable("LOADPATH_TRACE") == "1" ? LogLevel.Trace : LogLevel.Debug)
                .AddFilter("Uno", LogLevel.Warning)
                .AddFilter("Windows", LogLevel.Warning)
                .AddFilter("Microsoft", LogLevel.Warning))
            .UseThemeSwitching()
            .ConfigureServices(services => services.AddLoadpath(window, ui)));
        MainWindow = window;
#if DEBUG && !NO_UNO_STUDIO
        // Headless verification runs opt out: UseStudio() keeps the window hidden when no DevServer is reachable.
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
        {
            MainWindow.UseStudio();
        }
#endif
        MainWindow.SetWindowIcon();
        MainWindow.Title = "Loadpath";

        Host = builder.Build();
        _windowScope = Host.Services.CreateScope();
        ReportUnhandledFailures(Host.Services);

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
            rootFrame.Loaded += OnContentLoaded;
        }
        Services.GetRequiredService<global::Loadpath.Services.CloseGuard>().Attach(MainWindow);

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        MainWindow.Activate();
    }

    // xaml-lint: allow codebehind - the launch size is asserted once the window content has a layout (no XAML surface)
    private void OnContentLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement content)
        {
            content.Loaded -= OnContentLoaded;
            if (MainWindow is { } window) SizeWindowAtLaunch(window, content);
        }
    }

    /// <summary>
    /// Every handler guards its own work (GuardAsync, RunAsync, ReportFailure). What still escapes is logged and shown as
    /// a toast instead of closing the app: the work is in the autosave, and the user can save it.
    /// </summary>
    private void ReportUnhandledFailures(IServiceProvider services)
    {
        var log = services.GetRequiredService<ILogger<App>>();
        var engine = services.GetRequiredService<EditorEngine>();
        var ui = services.GetRequiredService<IUiThread>();
        UnhandledException += (_, e) =>
        {
            log.LogError(e.Exception, "Unhandled exception");
            e.Handled = true;
            ui.Run(() => engine.ToastError($"Something went wrong: {e.Exception.Message}"));
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
            ui.Run(() => engine.ToastError($"A background task failed: {e.Exception.GetBaseException().Message}"));
        };
    }
}
