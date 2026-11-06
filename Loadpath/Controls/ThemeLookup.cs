using Windows.UI;

namespace Loadpath.Controls;

/// <summary>
/// Resolves a resource for an explicit theme: a role colour or brush of the Simple theme (<c>PrimaryColor</c>,
/// <c>OnSurfaceVariantBrush</c>: the palette is Styles/ColorPaletteOverride.xaml) or one of App.xaml's per-theme values
/// (<c>WarnColor</c>, the canvas alphas). Code has no {ThemeResource}, and a plain <c>Application.Current.Resources[key]</c>
/// answers with the application's theme, not the window's, so the canvas palette and the brush-key converters ask for the
/// theme they are drawing in. The walk goes through the merged dictionaries, the theme's generated palette included.
/// </summary>
public static class ThemeLookup
{
    /// <summary>The theme dictionary keys to try, in order. The dark palette is keyed "Default" (WinUI convention).</summary>
    private static string[] KeysFor(ElementTheme theme) => theme == ElementTheme.Dark ? ["Dark", "Default"] : ["Light"];

    /// <summary>
    /// Raised once after the theme recoloured role brushes that code has read: the palette itself changed (Uno Platform
    /// Studio's Themes tab editing Styles/ColorPaletteOverride.xaml under Hot Reload, or an override assigned to the
    /// theme at runtime). A Light/Dark switch is ActualThemeChanged; this is for the palette under the same theme.
    /// </summary>
    public static event EventHandler? PaletteChanged;

    /// <summary>
    /// A colour, or a brush's colour with its opacity folded into the alpha (the state brushes are a role at 8-64 %).
    /// A brush read here is watched: when the theme recolours it in place, <see cref="PaletteChanged"/> is raised.
    /// </summary>
    public static Color Color(string key, ElementTheme theme, Color fallback)
    {
        switch (Value(key, theme))
        {
            case Color c:
                return c;
            case SolidColorBrush b:
                Watch(b);
                return Windows.UI.Color.FromArgb((byte)Math.Round(b.Color.A * Math.Clamp(b.Opacity, 0, 1)), b.Color.R, b.Color.G, b.Color.B);
            default:
                return fallback;
        }
    }

    private static readonly HashSet<SolidColorBrush> Watched = new(ReferenceEqualityComparer.Instance);
    private static bool _paletteChangeQueued;

    private static void Watch(SolidColorBrush roleBrush)
    {
        if (Watched.Add(roleBrush)) roleBrush.RegisterPropertyChangedCallback(SolidColorBrush.ColorProperty, (_, _) => QueuePaletteChanged());
    }

    /// <summary>A palette change recolours many brushes; tell the readers once, after the theme has finished.</summary>
    private static void QueuePaletteChanged()
    {
        if (_paletteChangeQueued || Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread() is not { } queue) return;
        _paletteChangeQueued = true;
        queue.TryEnqueue(() =>
        {
            _paletteChangeQueued = false;
            PaletteChanged?.Invoke(null, EventArgs.Empty);
        });
    }

    public static double Double(string key, ElementTheme theme, double fallback) => Value(key, theme) is double d ? d : fallback;

    public static object? Value(string key, ElementTheme theme)
    {
        var resources = Application.Current?.Resources;
        if (resources is null) return null;
        foreach (var themeKey in KeysFor(theme))
        {
            if (FindThemed(resources, key, themeKey) is { } found) return found;
        }
        return resources.TryGetValue(key, out var direct) ? direct : null;
    }

    /// <summary>Depth-first through merged dictionaries, last first: a later dictionary wins in WinUI.</summary>
    private static object? FindThemed(ResourceDictionary dictionary, string key, string themeKey)
    {
        if (dictionary.ThemeDictionaries.TryGetValue(themeKey, out var t) && t is ResourceDictionary td && td.TryGetValue(key, out var v)) return v;
        for (var i = dictionary.MergedDictionaries.Count - 1; i >= 0; i--)
        {
            if (FindThemed(dictionary.MergedDictionaries[i], key, themeKey) is { } found) return found;
        }
        return null;
    }
}

/// <summary>
/// Brushes for the resource keys the model hands to the views ("ErrorBrush", "PrimaryContainerBrush", …). A converter cannot
/// see the element it converts for, and its result is not re-asked when the theme changes, so it hands out one shared
/// brush per key and <see cref="Apply"/> recolors every shared brush in place when the window's theme changes (and again
/// when the palette itself changes, <see cref="ThemeLookup.PaletteChanged"/>). The app
/// sets its theme on the window content only, so the window's theme is every element's theme.
/// </summary>
public static class ThemeBrushes
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new();
    private static ElementTheme _theme = ElementTheme.Light;

    static ThemeBrushes() => ThemeLookup.PaletteChanged += (_, _) => Recolor();

    public static ElementTheme Theme => _theme;

    public static Brush Get(string key)
    {
        if (!Cache.TryGetValue(key, out var brush))
        {
            brush = new SolidColorBrush(ThemeLookup.Color(key, _theme, Microsoft.UI.Colors.Transparent));
            Cache[key] = brush;
        }
        return brush;
    }

    /// <summary>Recolor every handed-out brush for <paramref name="theme"/> (Light or Dark).</summary>
    public static void Apply(ElementTheme theme)
    {
        if (theme == ElementTheme.Default) return;
        _theme = theme;
        Recolor();
    }

    /// <summary>Re-reads every handed-out brush for the current theme (after a theme or a palette change).</summary>
    private static void Recolor()
    {
        foreach (var (key, brush) in Cache) brush.Color = ThemeLookup.Color(key, _theme, Microsoft.UI.Colors.Transparent);
    }
}
