using System.Text;
using Loadpath.Core.Serialization;
using Loadpath.Presentation;
using Microsoft.Extensions.Logging;
using Windows.Storage.Pickers;
using Windows.Storage.Provider;

namespace Loadpath.Services;

/// <summary>
/// Open/save through the platform pickers, with a fallback folder on a host that has no picker. Every fallback is
/// reported to the caller, which says so; a picker that fails is logged, and the caller reports it.
/// Document I/O goes through <see cref="StorageFile"/> streams, never <c>file.Path</c>: a picked file's path is a
/// browser handle or a temporary copy on WebAssembly and a content URI on Android.
/// Pickers run on the UI thread; callers await from there.
/// </summary>
public sealed class FileService(Func<Window?> window, ILogger<FileService> log) : IDocumentFiles
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

    /// <summary>The window a WinAppSDK picker needs as its owner (read on Windows only).</summary>
    private readonly Func<Window?> _window = window;

    /// <summary>The file behind the open document and the key the editor holds as its FilePath.</summary>
    private (StorageFile File, string Key)? _current;

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
            catch (Exception ex)
            {
                // The callers report the missing folder (a save or a share code has nowhere to go); the cause is logged.
                log.LogWarning(ex, "No fallback folder for documents on this host");
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

    /// <summary>
    /// Pick a document and read it; null when cancelled. On a host without a picker (Linux with no portal) the newest
    /// document in the fallback folder is opened, and the result says so. A picker that fails is not a cancel and
    /// opens nothing: it throws, and the caller reports it.
    /// </summary>
    public async Task<OpenedDocument?> OpenAsync()
    {
        StorageFile? file;
        var fromFallback = !PickersAvailable;
        if (fromFallback)
        {
            var latest = LatestInFallbackFolder("*" + DocumentSerializer.Extension)
                ?? throw new FileNotFoundException($"there is no file picker here and no {DocumentSerializer.Extension} file in {FallbackFolderName}");
            file = await StorageFile.GetFileFromPathAsync(latest);
        }
        else
        {
            file = await PickOpenAsync();
            if (file is null) return null; // cancelled
        }

        var snapshot = DocumentSerializer.Deserialize(await ReadTextAsync(file));
        var key = KeyOf(file);
        _current = (file, key);
        return new OpenedDocument(snapshot, key, fromFallback);
    }

    /// <summary>
    /// Save to the document's file, or ask for one; null when cancelled. The key is what the editor keeps as its
    /// FilePath (a full path where the head has one, otherwise the file name). Without a picker, or when the picker
    /// fails, the file goes to the fallback folder and the result says so (and why).
    /// </summary>
    public async Task<SavedDocument?> SaveAsync(StructureDocument doc, string? existingPath, bool forceAsk)
    {
        var json = DocumentSerializer.Serialize(doc);
        if (!forceAsk && existingPath is not null && await ResolveAsync(existingPath) is { } existing)
        {
            await WriteTextAsync(existing, json);
            _current = (existing, existingPath);
            return new SavedDocument(existingPath, ToFallbackFolder: false, PickerError: null);
        }

        StorageFile? file = null;
        string? pickerError = null;
        if (PickersAvailable)
        {
            try
            {
                file = await PickSaveAsync(DocumentSerializer.SafeFileName(doc.Name));
                if (file is null) return null; // cancelled
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // The work is kept in the fallback folder instead, and the caller reports the failure with where it went.
                log.LogWarning(ex, "The save picker failed; saving to the fallback folder");
                pickerError = ex.Message;
            }
        }
        var toFallback = file is null;
        if (file is null)
        {
            var folder = FallbackFolder ?? throw new IOException(pickerError is null
                ? "there is no file picker or local folder on this device"
                : $"the file picker failed ({pickerError}) and there is no local folder to save to");
            file = await CreateFileAsync(Path.Combine(folder, DocumentSerializer.SafeFileName(doc.Name) + DocumentSerializer.Extension));
        }

        await WriteTextAsync(file, json);
        var key = KeyOf(file);
        _current = (file, key);
        return new SavedDocument(key, toFallback, pickerError);
    }

    /// <summary>The fallback folder as a toast names it ("Documents/Loadpath").</summary>
    private string FallbackFolderName => FallbackFolder is { } f ? ToastText.FileInFolder(f) : "a local folder";

    /// <summary>The newest file matching the pattern in the fallback folder, as a full path.</summary>
    public string? LatestInFallbackFolder(string pattern)
    {
        var folder = FallbackFolder;
        if (folder is null) return null;
        return Directory.GetFiles(folder, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    /// <summary>The picked file, or null when cancelled. A picker that fails throws an IOException that says so, with the cause inside.</summary>
    private async Task<StorageFile?> PickOpenAsync()
    {
        try
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(DocumentSerializer.Extension);
            picker.FileTypeFilter.Add(".json");
            InitializeWithWindow(picker);
            return await picker.PickSingleFileAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new IOException($"the file picker failed ({ex.Message})", ex);
        }
    }

    /// <summary>The file picked to save to, or null when cancelled. A failure propagates to <see cref="SaveAsync"/>.</summary>
    private async Task<StorageFile?> PickSaveAsync(string suggestedName)
    {
        var picker = new FileSavePicker { SuggestedFileName = suggestedName };
        picker.FileTypeChoices.Add("Loadpath structure", new List<string> { DocumentSerializer.Extension });
        InitializeWithWindow(picker);
        return await picker.PickSaveFileAsync();
    }

    /// <summary>The file behind a FilePath key: the picked file, or a real path reopened (and recreated if it was deleted).</summary>
    private async Task<StorageFile?> ResolveAsync(string key)
    {
        if (_current is { } current && current.Key == key) return current.File;
        // Only a full path on a file system the app can reach survives a restart (desktop, Windows, the fallback
        // folder). A file picked in an earlier session on WebAssembly or Android is known by name only: ask again.
        if (!Path.IsPathRooted(key) || Path.GetDirectoryName(key) is not { } dir || !Directory.Exists(dir)) return null;
        try
        {
            return await CreateFileAsync(key);
        }
        catch (Exception ex)
        {
            // The save asks where to save instead, which the user sees; why the file could not be reopened is logged.
            log.LogWarning(ex, "Could not reopen {Path}; asking where to save", key);
            return null;
        }
    }

    private static async Task<StorageFile> CreateFileAsync(string path)
    {
        var dir = Path.GetDirectoryName(path) ?? throw new IOException($"{path} is not in a folder");
        var folder = await StorageFolder.GetFolderFromPathAsync(dir);
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

    private void InitializeWithWindow(object picker)
    {
#if WINDOWS
        // WinAppSDK pickers throw without an owner window.
        var owner = _window();
        if (owner is not null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(owner);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }
#endif
    }
}
