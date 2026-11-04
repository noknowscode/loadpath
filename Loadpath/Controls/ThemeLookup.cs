using Windows.UI;

namespace Loadpath.Controls;

/// <summary>
/// Resolves a token from Themes/Tokens.xaml for an explicit theme. Code has no {ThemeResource}, and a plain
/// <c>Application.Current.Resources[key]</c> answers with the application's theme, not the window's, so the canvas
/// palette and the brush-key converters ask for the theme they are drawing in.
/// </summary>
public static class ThemeLookup
{
    /// <summary>The theme dictionary keys to try, in order. Tokens.xaml keys its dark palette "Default" (WinUI convention).</summary>
    private static string[] KeysFor(ElementTheme theme) => theme == ElementTheme.Dark ? ["Dark", "Default"] : ["Light"];

    public static Color Color(string key, ElementTheme theme, Color fallback) => Value(key, theme) switch
    {
        Color c => c,
        SolidColorBrush b => b.Color,
        _ => fallback,
    };

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
/// Brushes for the resource keys the model hands to the views ("DangerBrush", "SelectionBrush", …). A converter cannot
/// see the element it converts for, and its result is not re-asked when the theme changes, so it hands out one shared
/// brush per key and <see cref="Apply"/> recolors every shared brush in place when the window's theme changes. The app
/// sets its theme on the window content only, so the window's theme is every element's theme.
/// </summary>
public static class ThemeBrushes
{
    private static readonly Dictionary<string, SolidColorBrush> Cache = new();
    private static ElementTheme _theme = ElementTheme.Light;

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
        foreach (var (key, brush) in Cache) brush.Color = ThemeLookup.Color(key, theme, Microsoft.UI.Colors.Transparent);
    }
}
