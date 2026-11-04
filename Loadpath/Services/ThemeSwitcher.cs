using Loadpath.Controls;
using Loadpath.Presentation;
using Microsoft.Extensions.Logging;
using Uno.Extensions.Toolkit;

namespace Loadpath.Services;

/// <summary>
/// Puts the theme choice (View options, saved in settings.json) on the window through the Extensions theme service,
/// which sets it on the window content, so every page element, flyout and dialog resolves its {ThemeResource} brushes
/// from one place; System leaves it unset and the app follows the platform, live. The service can only apply a theme
/// once the content is loaded (a call before that never completes), so <see cref="StartAsync"/> runs from the page's
/// Loaded. Until then, and on every change after, the brushes that code hands out (the canvas palette, the converters'
/// shared brushes) are recolored here.
/// </summary>
public sealed class ThemeSwitcher
{
    private readonly EditorEngine _engine;
    private readonly IThemeService _themes;
    private readonly ILogger _log;
    private bool _started;

    public ThemeSwitcher(EditorEngine engine, IThemeService themes, ILogger<ThemeSwitcher> log)
    {
        _engine = engine;
        _themes = themes;
        _log = log;
        engine.OptionsChanged += (_, name) =>
        {
            if (name != nameof(ViewOptions.Theme)) return;
            if (_started) _ = ApplyAsync();
            else ThemeBrushes.Apply(Expected());
        };
    }

    /// <summary>Before the views exist: recolor the code-made brushes for the theme the window will have.</summary>
    public void PrepareBrushes() => ThemeBrushes.Apply(Expected());

    /// <summary>Once the page is loaded: apply the choice, and follow the page's actual theme (live system changes under System).</summary>
    public async Task StartAsync(FrameworkElement page)
    {
        page.ActualThemeChanged += (_, _) => ThemeBrushes.Apply(page.ActualTheme);
        await _themes.InitializeAsync();
        _started = true;
        await ApplyAsync();
        ThemeBrushes.Apply(page.ActualTheme);
    }

    private async Task ApplyAsync()
    {
        var theme = _engine.Options.Theme switch
        {
            ThemeChoice.Light => AppTheme.Light,
            ThemeChoice.Dark => AppTheme.Dark,
            _ => AppTheme.System,
        };
        try
        {
            if (!await _themes.SetThemeAsync(theme)) _log.LogWarning("The theme service did not apply {Theme}", theme);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Applying the {Theme} theme failed", theme);
            _engine.ToastError($"Theme not applied: {ex.Message}");
        }
    }

    /// <summary>The theme the window will resolve: the choice, or the application's (the platform's) under System.</summary>
    private ElementTheme Expected() => _engine.Options.Theme switch
    {
        ThemeChoice.Light => ElementTheme.Light,
        ThemeChoice.Dark => ElementTheme.Dark,
        _ => Application.Current.RequestedTheme == ApplicationTheme.Dark ? ElementTheme.Dark : ElementTheme.Light,
    };
}
