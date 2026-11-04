using System.Text.Json;
using Loadpath.Commands;
using Loadpath.Core.Serialization;
using Loadpath.Services;
using Microsoft.Extensions.Logging;

namespace Loadpath.Presentation;

/// <summary>
/// The open document's life outside the editor: the file it belongs to; new, open, save and save as; share codes; the
/// autosave (debounced after each change) and its restore; and the unsaved-changes question before anything drops work.
/// Plain C#: the pickers (<see cref="IDocumentFiles"/>), the question (<see cref="IUnsavedPrompt"/>) and the UI thread's
/// timer come in through the constructor, so the tests hand it their own.
/// </summary>
public sealed class DocumentSession
{
    /// <summary>How long the document must stay unchanged before the autosave writes it.</summary>
    public static readonly TimeSpan AutosaveDelay = TimeSpan.FromMilliseconds(1200);

    private readonly EditorEngine _engine;
    private readonly IDocumentFiles _files;
    private readonly string _autosavePath;
    private readonly IUnsavedPrompt _prompt;
    private readonly ILogger _log;
    private readonly IUiTimer _autosaveTimer;
    private bool _autosaveDropped;
    private bool _autosaveFailing;

    public DocumentSession(EditorEngine engine, IDocumentFiles files, ISettingsStore store, IUnsavedPrompt prompt, IUiThread ui, ILogger<DocumentSession> log)
    {
        _engine = engine;
        _files = files;
        _autosavePath = store.AutosavePath;
        _prompt = prompt;
        _log = log;
        _autosaveTimer = ui.CreateTimer(AutosaveDelay, () => _ = RunAsync("Autosave", AutosaveAsync));
        // Every edit, and every save (which changes what the autosave records, the file and the clean state, without
        // changing the document), pushes the autosave back until the document has been still for a moment.
        engine.AnalysisChanged += (_, _) => _autosaveTimer.Restart();
        engine.SavedStateChanged += (_, _) => _autosaveTimer.Restart();
        RegisterCommands();
    }

    /// <summary>The File commands, for shortcuts, menus and the palette. Commands are synchronous, so each runs as a task.</summary>
    private void RegisterCommands()
    {
        var c = _engine.Commands;
        c.Add(new AppCommand("file.new", "New structure", "File", "Ctrl+N", () => _ = RunAsync("New", NewAsync), icon: "new"));
        c.Add(new AppCommand("file.open", "Open…", "File", "Ctrl+O", () => _ = RunAsync("Open", OpenAsync), icon: "folder"));
        c.Add(new AppCommand("file.save", "Save", "File", "Ctrl+S", () => _ = RunAsync("Save", () => SaveAsync(ask: false)), icon: "save"));
        c.Add(new AppCommand("file.saveAs", "Save as…", "File", "Ctrl+Shift+S", () => _ = RunAsync("Save as", () => SaveAsync(ask: true)), icon: "save"));
        c.Add(new AppCommand("file.share", "Copy share code", "File", "Ctrl+Shift+C", () => _ = RunAsync("Copy share code", ShareAsync), () => _engine.Document.Nodes.Count > 0, "duplicate"));
        c.Add(new AppCommand("file.paste", "Paste design from share code", "File", "Ctrl+Shift+V", () => _ = RunAsync("Paste", PasteAsync), icon: "folder"));
    }

    /// <summary>Any failure ends as a toast, never as an unobserved task exception.</summary>
    private async Task RunAsync(string action, Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "{Action} failed", action);
            _engine.ToastError($"{action} failed: {ex.Message}");
        }
    }

    public async Task NewAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        _engine.NewDocument();
    }

    public async Task OpenAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        try
        {
            var opened = await _files.OpenAsync();
            if (opened is null) { _engine.Toast("Nothing opened"); return; }
            _engine.LoadSnapshot(opened.Snapshot, "Open", opened.Path);
            _engine.Toast(opened.FromFallbackFolder
                ? $"No file picker here: opened the newest file, {ToastText.FileInFolder(opened.Path)}"
                : $"Opened {ToastText.FileName(opened.Path)}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Open failed");
            _engine.ToastError($"Could not open: {ex.Message}");
        }
    }

    /// <param name="ask">Save as: pick a file even when the document has one.</param>
    public async Task SaveAsync(bool ask)
    {
        try
        {
            var saved = await _files.SaveAsync(_engine.Document, _engine.FilePath, ask);
            if (saved is null) return;
            var path = saved.Path;
            _engine.FilePath = path;
            _engine.Document.Name = Path.GetFileNameWithoutExtension(path);
            _engine.MarkSaved();
            _log.LogInformation("Saved {Path}", path);
            if (saved.PickerError is { } error) _engine.ToastError($"The file picker failed ({error}). Saved to {ToastText.FileInFolder(path)} instead");
            else if (saved.ToFallbackFolder) _engine.Toast($"No file picker here: saved to {ToastText.FileInFolder(path)}");
            else _engine.Toast($"Saved {ToastText.FileName(path)}");
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Save failed");
            _engine.ToastError($"Could not save: {ex.Message}");
        }
    }

    /// <summary>
    /// Copy the document as a one-line share code. Hosts without a working clipboard get the code written to the
    /// fallback folder instead (Documents/Loadpath, or the app's local folder on WebAssembly, Android and iOS), and the
    /// toast says where. With neither, the toast says so.
    /// </summary>
    public async Task ShareAsync()
    {
        var code = ShareCode.Encode(_engine.Document);
        var size = code.Length < 1024 ? $"{code.Length} characters" : $"{code.Length / 1024.0:0.0} kB";
        try
        {
            var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
            package.SetText(code);
            Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
#if __DESKTOP__ || WINDOWS
            // Desktop hosts keep the code on the clipboard after Loadpath closes. The browser and the mobile
            // clipboards hold the text themselves, so there is nothing to flush there.
            Windows.ApplicationModel.DataTransfer.Clipboard.Flush();
#endif
            _engine.Toast($"Share code copied ({size}). Paste it in Loadpath with Ctrl+Shift+V.");
            return;
        }
        catch (Exception ex)
        {
            // No clipboard on this host: the code goes to the share file, and the toast says so.
            _log.LogInformation(ex, "No clipboard for the share code");
        }
        if (_files.FallbackFolder is not { } folder)
        {
            _engine.ToastError("No clipboard here, and no folder to save the share code to");
            return;
        }
        var path = Path.Combine(folder, DocumentSerializer.SafeFileName(_engine.Document.Name) + ShareFileSuffix);
        await File.WriteAllTextAsync(path, code);
        _log.LogInformation("Share code written to {Path}", path);
        _engine.Toast($"No clipboard here. Share code saved to {ToastText.FileInFolder(path)}");
    }

    private const string ShareFileSuffix = ".share.txt";

    /// <summary>Replace the document with a pasted design, as one undo step. Text that is no design leaves it alone and says why.</summary>
    public async Task PasteAsync()
    {
        string? text = null;
        try
        {
            var content = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
            if (content.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text)) text = await content.GetTextAsync();
        }
        catch (Exception ex)
        {
            // No clipboard on this host: read the most recent share file written by ShareAsync.
            _log.LogInformation(ex, "No clipboard to paste from; reading the newest share file");
            var file = _files.LatestInFallbackFolder("*" + ShareFileSuffix);
            if (file is not null) text = await File.ReadAllTextAsync(file);
        }
        if (!ShareCode.TryDecode(text, out var snapshot, out var error)) { _engine.Toast(error); return; }
        _engine.ReplaceDocument(snapshot, "Paste design");
        _engine.Toast($"Pasted {_engine.Document.Name} · Ctrl+Z to undo");
    }

    /// <summary>
    /// True when the open document may be dropped: nothing is unsaved, or the user saved or discarded it.
    /// </summary>
    /// <param name="closing">The window is closing: Discard also drops the autosave, so the next launch does not bring the discarded edits back.</param>
    public async Task<bool> ConfirmDiscardAsync(bool closing = false)
    {
        if (!_engine.IsDirty) return true;
        switch (await _prompt.AskAsync(_engine.Document.Name))
        {
            case UnsavedChoice.Save:
                await SaveAsync(ask: false);
                return !_engine.IsDirty;
            case UnsavedChoice.Discard:
                if (closing) DropAutosave();
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Bring back the autosaved session: the document, its file and its unsaved changes. Returns false, leaving the
    /// editor as it is, when there is no autosave or it holds nothing usable.
    /// </summary>
    public async Task<bool> RestoreAsync()
    {
        if (!File.Exists(_autosavePath)) return false;
        try
        {
            var session = Autosave.Deserialize(await File.ReadAllTextAsync(_autosavePath));
            if (session.Snapshot.Nodes.Count == 0) return false;
            _engine.RestoreSession(session);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or JsonException)
        {
            // A bad autosave is discarded: the next edit overwrites it.
            _log.LogWarning(ex, "The autosave at {Path} could not be restored", _autosavePath);
            return false;
        }
    }

    /// <summary>
    /// Write the autosave now (the timer calls this once edits pause). Nothing is written once closing discarded the work.
    /// A write that fails is reported when the failures start, not on every edit after; the next edit tries again.
    /// </summary>
    public async Task AutosaveAsync()
    {
        if (_autosaveDropped) return;
        // Serialize on the UI thread (the document is UI-owned); write off it. The file and the dirty flag go with the
        // document, so a restart restores the session as it was, not as the last file had it.
        var json = Autosave.Serialize(_engine.Document, _engine.FilePath, _engine.IsDirty);
        try
        {
            await File.WriteAllTextAsync(_autosavePath, json);
            _autosaveFailing = false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            if (!_autosaveFailing)
            {
                _log.LogWarning(ex, "Autosave to {Path} failed", _autosavePath);
                _engine.ToastError($"Autosave failed: {ex.Message.TrimEnd('.')}. Save to keep your work.");
            }
            _autosaveFailing = true;
        }
    }

    private void DropAutosave()
    {
        _autosaveDropped = true;
        _autosaveTimer.Stop();
        try { File.Delete(_autosavePath); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { /* nothing to drop */ }
    }
}
