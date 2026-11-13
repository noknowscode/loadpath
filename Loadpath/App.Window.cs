namespace Loadpath;

public partial class App
{
    public const int LaunchWidth = 1440;
    public const int LaunchHeight = 960;

    /// <summary>
    /// Desktop (X11, Win32, macOS) and WinAppSDK open at 1440 x 960. WebAssembly, Android and iOS fill the browser or
    /// the screen, so there is nothing to size.
    /// </summary>
    /// <param name="content">The window content, loaded: its size shows whether the host has caught up with the frame.</param>
    private static void SizeWindowAtLaunch(Window window, FrameworkElement content)
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("LOADPATH_NO_RESIZE") == "1") return;
#endif
#if __DESKTOP__
        // The window is sized through PreferredLaunchViewSize at launch. The X11 host can still miss a configure
        // event and lay out at the old size, so the size is re-asserted until the layout agrees with the frame.
        var attempts = 0;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        timer.Tick += (_, _) =>
        {
            attempts++;
            try
            {
                var size = window.AppWindow.Size;
                var layoutAgrees = Math.Abs(content.ActualWidth - size.Width) < 4 && Math.Abs(content.ActualHeight - size.Height) < 4;
                if (size.Width >= LaunchWidth - 2 && size.Height >= LaunchHeight - 2 && layoutAgrees) { timer.Stop(); return; }
                if (size.Width >= LaunchWidth - 2 && size.Height >= LaunchHeight - 2)
                {
                    // Frame is right but the layout is stale: nudge by a pixel so the host re-reads its size.
                    window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = size.Width + (attempts % 2 == 0 ? 1 : -1), Height = size.Height });
                }
                else
                {
                    window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = LaunchWidth, Height = LaunchHeight });
                }
            }
            catch (Exception)
            {
                // Cosmetic: the window keeps the size the host gave it.
                timer.Stop();
            }
            if (attempts >= 10) timer.Stop();
            timer.Interval = TimeSpan.FromMilliseconds(400);
        };
        timer.Start();
#elif WINDOWS
        // WinAppSDK has no launch-size preference: resize the AppWindow once, in physical pixels at this display's scale.
        try
        {
            var scale = content.XamlRoot?.RasterizationScale ?? 1.0;
            window.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = (int)(LaunchWidth * scale), Height = (int)(LaunchHeight * scale) });
        }
        catch (Exception)
        {
            // Cosmetic: the window keeps the size the system gave it.
        }
#endif
    }
}
