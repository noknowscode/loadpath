using System.Text.Json;
using System.Text.Json.Serialization;
using Loadpath.Presentation;

namespace Loadpath.Services;

/// <summary>The saved view options, as they are written to settings.json.</summary>
public sealed class SavedSettings
{
    public string DisplayMode { get; set; } = "Forces";
    public bool ShowDeflection { get; set; } = true;
    public double Exaggeration { get; set; } = 1;
    public bool ShowLabels { get; set; } = true;
    public bool ShowReactions { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public bool SnapEnabled { get; set; } = true;
    public double GridStep { get; set; } = 0.5;
    public bool InspectorVisible { get; set; } = true;
    public bool ReduceNoise { get; set; }
    public int NoiseKeep { get; set; } = (int)ViewOptions.DefaultKeep;
    /// <summary>"System", "Light" or "Dark". Missing in older settings files, which then follow the system.</summary>
    public string Theme { get; set; } = nameof(ThemeChoice.System);
}

/// <summary>Where the settings and the autosave live: the app's local folder (SettingsService), a temp folder in the tests.</summary>
public interface ISettingsStore
{
    /// <summary>The autosave file, next to the settings.</summary>
    string AutosavePath { get; }

    /// <summary>Read the settings now: on every head but WebAssembly, the local folder is there at launch. Defaults when there are none.</summary>
    SavedSettings Load();

    /// <summary>Read the settings once the local folder can be read (WebAssembly mounts it after launch). Read the autosave after this too.</summary>
    Task<SavedSettings> LoadAsync();

    /// <summary>Write the settings. Returns why they could not be written, or null when they were.</summary>
    string? Save(SavedSettings settings);
}

/// <summary>View options and the autosave, as files in the app's local folder.</summary>
public sealed class SettingsService : ISettingsStore
{
    /// <summary>Source-generated: trimmed heads (WebAssembly) turn reflection-based System.Text.Json off.</summary>
    private static readonly SettingsJsonContext Json = new(new JsonSerializerOptions { WriteIndented = true });

    public static string Folder
    {
        get
        {
            string? dir = null;
            try { dir = ApplicationData.Current.LocalFolder.Path; } catch { /* not available on every host */ }
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Loadpath");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }

    private const string SettingsFileName = "settings.json";
    private static string SettingsPath => Path.Combine(Folder, SettingsFileName);
    public string AutosavePath => Path.Combine(Folder, "autosave.loadpath");

    public SavedSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath)) return JsonSerializer.Deserialize(File.ReadAllText(SettingsPath), Json.SavedSettings) ?? new SavedSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            // Unreadable or corrupt settings start from the defaults; the next option change rewrites the file.
        }
        return new SavedSettings();
    }

    /// <summary>
    /// WebAssembly mounts the browser's persistent storage after launch, and a System.IO read before that finds nothing;
    /// an awaited StorageFolder call waits for the mount
    /// (https://platform.uno/docs/articles/features/file-management.html#webassembly-file-system).
    /// </summary>
    public async Task<SavedSettings> LoadAsync()
    {
        await ApplicationData.Current.LocalFolder.TryGetItemAsync(SettingsFileName);
        return Load();
    }

    public string? Save(SavedSettings settings)
    {
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, Json.SavedSettings));
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return ex.Message;
        }
    }
}

[JsonSerializable(typeof(SavedSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
