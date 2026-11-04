using System.Text.Json;
using Loadpath.Commands;
using Loadpath.Core.Serialization;
using Loadpath.Services;

namespace Loadpath.Presentation;

/// <summary>The answer to "this document has unsaved changes".</summary>
public enum UnsavedChoice { Save, Discard, Cancel }

/// <summary>
/// The open document's life outside the editor: the file it belongs to; new, open, save and save as; share codes; the
/// autosave and its restore; and the unsaved-changes question before anything drops work. Plain C#: the page hands it
/// the pickers (<see cref="IDocumentFiles"/>) and the question (a dialog), so the tests can hand it their own.
/// </summary>
public sealed class DocumentSession
{
    private readonly EditorEngine _engine;
    private readonly IDocumentFiles _files;
    private readonly string _autosavePath;
    private readonly Func<string, Task<UnsavedChoice>> _askUnsaved;
    private bool _autosaveDropped;

    /// <param name="askUnsaved">Asks about unsaved changes in the named document.</param>
    public DocumentSession(EditorEngine engine, IDocumentFiles files, string autosavePath, Func<string, Task<UnsavedChoice>> askUnsaved)
    {
        _engine = engine;
        _files = files;
        _autosavePath = autosavePath;
        _askUnsaved = askUnsaved;
    }

    /// <summary>The File commands, for shortcuts, menus and the palette. Commands are synchronous, so each runs as a task.</summary>
    public void RegisterCommands()
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
            _engine.Toast($"{action} failed: {ex.Message}");
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
            var result = await _files.OpenAsync();
            if (result is null) { _engine.Toast("Nothing opened"); return; }
            _engine.LoadSnapshot(result.Value.Snapshot, "Open", result.Value.Path);
            _engine.Toast($"Opened {Path.GetFileName(result.Value.Path)}");
        }
        catch (Exception ex)
        {
            _engine.Toast($"Could not open: {ex.Message}");
        }
    }

    /// <param name="ask">Save as: pick a file even when the document has one.</param>
    public async Task SaveAsync(bool ask)
    {
        try
        {
            var path = await _files.SaveAsync(_engine.Document, _engine.FilePath, ask);
            if (path is null) return;
            _engine.FilePath = path;
            _engine.Document.Name = Path.GetFileNameWithoutExtension(path);
            _engine.MarkSaved();
            _engine.Toast($"Saved to {path}");
        }
        catch (Exception ex)
        {
            _engine.Toast($"Could not save: {ex.Message}");
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
        catch (Exception)
        {
            // No clipboard on this host: fall through to the share file.
        }
        if (_files.FallbackFolder is not { } folder)
        {
            _engine.Toast("No clipboard here, and no folder to save the share code to");
            return;
        }
        var path = Path.Combine(folder, DocumentSerializer.SafeFileName(_engine.Document.Name) + ShareFileSuffix);
        await File.WriteAllTextAsync(path, code);
        _engine.Toast($"No clipboard here. Share code saved to {path}");
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
        catch (Exception)
        {
            // No clipboard on this host: read the most recent share file written by ShareAsync.
            var file = _files.LatestInFallbackFolder("*" + ShareFileSuffix);
            if (file is not null) text = await File.ReadAllTextAsync(file);
        }
        if (!ShareCode.TryDecode(text, out var snapshot, out var error)) { _engine.Toast(error); return; }
        _engine.ReplaceDocument(snapshot!, "Paste design");
        _engine.Toast($"Pasted {_engine.Document.Name} · Ctrl+Z to undo");
    }

    /// <summary>
    /// True when the open document may be dropped: nothing is unsaved, or the user saved or discarded it.
    /// </summary>
    /// <param name="closing">The window is closing: Discard also drops the autosave, so the next launch does not bring the discarded edits back.</param>
    public async Task<bool> ConfirmDiscardAsync(bool closing = false)
    {
        if (!_engine.IsDirty) return true;
        switch (await _askUnsaved(_engine.Document.Name))
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
            return false;
        }
    }

    /// <summary>Write the autosave now; the page debounces the calls. Nothing is written once closing discarded the work.</summary>
    public async Task AutosaveAsync()
    {
        if (_autosaveDropped) return;
        // Serialize on the UI thread (the document is UI-owned); write off it. The file and the dirty flag go with the
        // document, so a restart restores the session as it was, not as the last file had it.
        var json = Autosave.Serialize(_engine.Document, _engine.FilePath, _engine.IsDirty);
        await File.WriteAllTextAsync(_autosavePath, json);
    }

    private void DropAutosave()
    {
        _autosaveDropped = true;
        try { File.Delete(_autosavePath); } catch (Exception) { /* nothing to drop */ }
    }
}
