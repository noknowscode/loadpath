namespace Loadpath;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
    }

    public static Window? MainWindow { get; private set; }
    public const int LaunchWidth = 1440;
    public const int LaunchHeight = 960;

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
#if __DESKTOP__
        // Size the window at creation; resizing after the fact races the Skia host's surface.
        // Desktop only: WebAssembly, Android and iOS fill the browser or the screen, and WinAppSDK sizes its
        // AppWindow once the page knows the display scale (MainPage.OnLoaded).
        Windows.UI.ViewManagement.ApplicationView.PreferredLaunchViewSize = new Windows.Foundation.Size(LaunchWidth, LaunchHeight);
#endif
        MainWindow = new Window();
#if DEBUG && !NO_UNO_STUDIO
        // Headless verification runs opt out: UseStudio() keeps the window hidden when no DevServer is reachable.
        if (Environment.GetEnvironmentVariable("APP_NO_HOTDESIGN") != "1")
        {
            MainWindow.UseStudio();
        }
#endif
        MainWindow.SetWindowIcon();
        MainWindow.Title = "Loadpath";

        if (MainWindow.Content is not Frame rootFrame)
        {
            rootFrame = new Frame();
            MainWindow.Content = rootFrame;
        }

        if (rootFrame.Content is null)
        {
            rootFrame.Navigate(typeof(MainPage), args.Arguments);
        }

        MainWindow.Activate();
    }
}
