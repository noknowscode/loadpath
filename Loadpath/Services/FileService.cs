using System.Text;
using Loadpath.Core.Editing;
using Loadpath.Core.Serialization;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;

namespace Loadpath.Services;

/// <summary>
/// Open/save through the platform pickers, with a fallback folder when a picker is unavailable on the host.
/// Document I/O goes through <see cref="StorageFile"/> streams, never <c>file.Path</c>: a picked file's path is a
/// browser handle or a temporary copy on WebAssembly and a content URI on Android.
/// Pickers run on the UI thread; callers await from there.
/// </summary>
public sealed class FileService
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    private readonly Func<Window?> _window;

    /// <summary>The file behind the open document and the key the editor holds as its FilePath.</summary>
    private (StorageFile File, string Key)? _current;

    public FileService(Func<Window?> window) => _window = window;

    /// <summary>
    /// Where files go when no picker works: Documents/Loadpath on desktop and Windows, the app's local folder on heads
    /// without a user Documents folder (WebAssembly, Android, iOS). Null when the folder can't be created.
    /// </summary>
    public string? FallbackFolder
    {
        get
        {
            try
            {
#if __DESKTOP__ || WINDOWS
                var docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (string.IsNullOrEmpty(docs) || docs == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
                    docs = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Documents");
                var dir = Path.Combine(docs, "Loadpath");
#else
                var dir = Path.Combine(ApplicationData.Current.LocalFolder.Path, "Documents");
#endif
                Directory.CreateDirectory(dir);
                return dir;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// On Linux the pickers go through the xdg-desktop-portal over the D-Bus session bus.
    /// Without a session bus (containers, bare X sessions) the call never completes, so the fallback is used up front.
    /// </summary>
    public static bool PickersAvailable
    {
        get
        {
            if (!OperatingSystem.IsLinux()) return true;
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("DBUS_SESSION_BUS_ADDRESS"))) return true;
            var runtime = Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
            return !string.IsNullOrEmpty(runtime) && File.Exists(Path.Combine(runtime, "bus"));
        }
    }

    /// <summary>Pick a document and read it. Null when cancelled or when there is nothing to open.</summary>
    public async Task<(DocumentSnapshot Snapshot, string Path)?> OpenAsync()
    {
        var (file, unavailable) = PickersAvailable ? await PickOpenAsync() : (null, true);
        if (file is null)
        {
            if (!unavailable) return null; // cancelled
            // Picker unavailable on this host: open the most recent file in the fallback folder.
            var latest = LatestInFallbackFolder("*" + DocumentSerializer.Extension);
            if (latest is null) return null;
            file = await StorageFile.GetFileFromPathAsync(latest);
        }

        var snapshot = DocumentSerializer.Deserialize(await ReadTextAsync(file));
        var key = KeyOf(file);
        _current = (file, key);
        return (snapshot, key);
    }

    /// <summary>
    /// Save to the document's file, or ask for one. Returns the key the editor keeps as its FilePath (a full path
    /// where the head has one, otherwise the file name), or null when cancelled.
    /// </summary>
    public async Task<string?> SaveAsync(StructureDocument doc, string? existingPath, bool forceAsk)
    {
        var json = DocumentSerializer.Serialize(doc);
        if (!forceAsk && existingPath is not null && await ResolveAsync(existingPath) is { } existing)
        {
            await WriteTextAsync(existing, json);
            _current = (existing, existingPath);
            return existingPath;
        }

        var (file, unavailable) = PickersAvailable ? await PickSaveAsync(Sanitize(doc.Name)) : (null, true);
        if (file is null)
        {
            if (!unavailable) return null; // cancelled
            var folder = FallbackFolder ?? throw new IOException("there is no file picker or local folder on this device");
            file = await CreateFileAsync(Path.Combine(folder, Sanitize(doc.Name) + DocumentSerializer.Extension));
        }

        await WriteTextAsync(file, json);
        var key = KeyOf(file);
        _current = (file, key);
        return key;
    }

    /// <summary>The newest file matching the pattern in the fallback folder, as a full path.</summary>
    public string? LatestInFallbackFolder(string pattern)
    {
        var folder = FallbackFolder;
        if (folder is null) return null;
        return Directory.GetFiles(folder, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>The picked file, or null with Unavailable set when the picker threw (as opposed to being cancelled).</summary>
    private async Task<(StorageFile? File, bool Unavailable)> PickOpenAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(DocumentSerializer.Extension);
            picker.FileTypeFilter.Add(".json");
            InitializeWithWindow(picker);
            return (await picker.PickSingleFileAsync(), false);
        }
        catch (Exception)
        {
            return (null, true);
        }
    }

    private async Task<(StorageFile? File, bool Unavailable)> PickSaveAsync(string suggestedName)
    {
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = suggestedName };
            picker.FileTypeChoices.Add("Loadpath structure", new List<string> { DocumentSerializer.Extension });
            InitializeWithWindow(picker);
            return (await picker.PickSaveFileAsync(), false);
        }
        catch (Exception)
        {
            return (null, true);
        }
    }

    /// <summary>The file behind a FilePath key: the picked file, or a real path reopened (and recreated if it was deleted).</summary>
    private async Task<StorageFile?> ResolveAsync(string key)
    {
        if (_current is { } current && current.Key == key) return current.File;
        // Only a full path on a file system the app can reach survives a restart (desktop, Windows, the fallback
        // folder). A file picked in an earlier session on WebAssembly or Android is known by name only: ask again.
        if (!Path.IsPathRooted(key) || !Directory.Exists(Path.GetDirectoryName(key))) return null;
        try
        {
            return await CreateFileAsync(key);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static async Task<StorageFile> CreateFileAsync(string path)
    {
        var folder = await StorageFolder.GetFolderFromPathAsync(Path.GetDirectoryName(path)!);
        return await folder.CreateFileAsync(Path.GetFileName(path), CreationCollisionOption.OpenIfExists);
    }

    private static string KeyOf(StorageFile file) => string.IsNullOrEmpty(file.Path) ? file.Name : file.Path;

    private static async Task<string> ReadTextAsync(StorageFile file)
    {
        await using var stream = await file.OpenStreamForReadAsync();
        using var reader = new StreamReader(stream, Utf8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync();
    }

    private static async Task WriteTextAsync(StorageFile file, string text)
    {
        // WebAssembly's download picker hands out a temporary file and starts the browser download on
        // CompleteUpdatesAsync; the File System Access picker and the other heads complete at once.
        CachedFileManager.DeferUpdates(file);
        await using (var stream = await file.OpenStreamForWriteAsync())
        {
            stream.SetLength(0);
            await using var writer = new StreamWriter(stream, Utf8);
            await writer.WriteAsync(text);
        }
        var status = await CachedFileManager.CompleteUpdatesAsync(file);
        if (status == FileUpdateStatus.Failed) throw new IOException($"{file.Name} could not be written");
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var clean = new string(name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray()).Trim();
        return string.IsNullOrEmpty(clean) ? "Untitled" : clean;
    }

    private void InitializeWithWindow(object picker)
    {
#if WINDOWS
        // WinAppSDK pickers throw without an owner window.
        var window = _window();
        if (window is not null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
#endif
    }
}
