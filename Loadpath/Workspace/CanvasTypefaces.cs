using SkiaSharp;
using Windows.Storage;

namespace Loadpath.Workspace;

/// <summary>
/// The canvas typefaces, read once from the app package so every head draws with the bundled fonts.
/// <c>ms-appx:///</c> resolves on all heads; a path under <see cref="AppContext.BaseDirectory"/> does not exist on
/// WebAssembly, Android or iOS, and the browser has no system fonts to fall back to.
/// A face that can't be read stays null and the renderer keeps its fallback for it.
/// </summary>
public sealed record CanvasTypefaces(SKTypeface? Mono, SKTypeface? MonoMedium, SKTypeface? Ui)
{
    private static Task<CanvasTypefaces>? _load;

    /// <summary>Starts the load on first call; later calls share it. Call from the UI thread.</summary>
    public static Task<CanvasTypefaces> LoadAsync() => _load ??= LoadAllAsync();

    private static async Task<CanvasTypefaces> LoadAllAsync()
    {
        var mono = ReadAsync("JetBrainsMono-Regular.ttf");
        var monoMedium = ReadAsync("JetBrainsMono-Medium.ttf");
        var ui = ReadAsync("Inter-Regular.ttf");
        return new CanvasTypefaces(await mono, await monoMedium, await ui);
    }

    private static async Task<SKTypeface?> ReadAsync(string file)
    {
        try
        {
            var storage = await StorageFile.GetFileFromApplicationUriAsync(new Uri($"ms-appx:///Assets/Fonts/{file}"));
            await using var stream = await storage.OpenStreamForReadAsync();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer);
            using var data = SKData.CreateCopy(buffer.ToArray());
            return SKTypeface.FromData(data);
        }
        catch (Exception)
        {
            // Missing or unreadable font: the canvas keeps drawing with the fallback face.
            return null;
        }
    }
}
