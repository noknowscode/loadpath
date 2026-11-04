using Loadpath.Core.Editing;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Loadpath.Core.Serialization;
using Loadpath.Presentation;
using Loadpath.Services;
using Xunit;

namespace Loadpath.Tests;

/// <summary>New, open, save, the unsaved-changes question, and the autosave across a restart.</summary>
public sealed class DocumentSessionTests : IDisposable
{
    private const string FileA = "/docs/bridge.loadpath";
    private const string FileB = "/docs/roof.loadpath";

    /// <summary>In-memory documents, a scripted picker, and a real fallback folder for share files.</summary>
    private sealed class Files(string fallbackFolder) : IDocumentFiles
    {
        public Dictionary<string, string> Disk { get; } = new();
        /// <summary>What the open picker returns next; null cancels.</summary>
        public string? PickToOpen { get; set; }
        /// <summary>What the save picker returns next; null cancels.</summary>
        public string? PickToSave { get; set; }
        public int SavePickerShown { get; private set; }

        public Task<(DocumentSnapshot Snapshot, string Path)?> OpenAsync() =>
            Task.FromResult(PickToOpen is { } path ? (DocumentSerializer.Deserialize(Disk[path]), path) : ((DocumentSnapshot, string)?)null);

        public Task<string?> SaveAsync(StructureDocument doc, string? existingPath, bool forceAsk)
        {
            var path = existingPath;
            if (forceAsk || path is null)
            {
                SavePickerShown++;
                path = PickToSave;
            }
            if (path is not null) Disk[path] = DocumentSerializer.Serialize(doc);
            return Task.FromResult(path);
        }

        public string? FallbackFolder => fallbackFolder;

        public string? LatestInFallbackFolder(string pattern) =>
            Directory.GetFiles(fallbackFolder, pattern).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
    }

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "loadpath-tests-" + Guid.NewGuid().ToString("N"));
    private string AutosavePath => Path.Combine(_folder, "autosave.loadpath");

    public DocumentSessionTests()
    {
        Directory.CreateDirectory(_folder);
        _files = new Files(_folder);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private readonly Files _files;
    private readonly List<string> _asked = new();
    private readonly List<string> _toasts = new();

    private (EditorEngine Engine, DocumentSession Session) Start(UnsavedChoice answer = UnsavedChoice.Cancel)
    {
        var engine = new EditorEngine();
        engine.ToastRequested += (_, message) => _toasts.Add(message);
        var session = new DocumentSession(engine, _files, AutosavePath, name =>
        {
            _asked.Add(name);
            return Task.FromResult(answer);
        });
        return (engine, session);
    }

    private void PutOnDisk(string path, string sample)
    {
        var doc = new StructureDocument();
        SampleStructures.Build(sample).Restore(doc);
        _files.Disk[path] = DocumentSerializer.Serialize(doc);
    }

    private static void Edit(EditorEngine engine) => engine.History.Do(new SetSupportEdit(4, SupportKind.Pin));

    [Fact]
    public async Task Open_attaches_the_file_and_starts_clean()
    {
        PutOnDisk(FileA, "warren");
        _files.PickToOpen = FileA;
        var (engine, session) = Start();

        await session.OpenAsync();

        Assert.Equal(FileA, engine.FilePath);
        Assert.Equal("Warren truss", engine.Document.Name);
        Assert.False(engine.IsDirty);
        Assert.Empty(_asked);
    }

    [Fact]
    public async Task Open_over_unsaved_work_asks_and_cancel_keeps_the_work()
    {
        PutOnDisk(FileA, "warren");
        PutOnDisk(FileB, "roof");
        _files.PickToOpen = FileA;
        var (engine, session) = Start(UnsavedChoice.Cancel);
        await session.OpenAsync();
        Edit(engine);

        _files.PickToOpen = FileB;
        await session.OpenAsync();

        Assert.Equal(new[] { "Warren truss" }, _asked);
        Assert.Equal(FileA, engine.FilePath);
        Assert.Equal(SupportKind.Pin, engine.Document.GetNode(4).Support);
        Assert.True(engine.IsDirty);
    }

    [Fact]
    public async Task Discard_lets_open_replace_the_work_without_saving_it()
    {
        PutOnDisk(FileA, "warren");
        PutOnDisk(FileB, "roof");
        var original = _files.Disk[FileA];
        _files.PickToOpen = FileA;
        var (engine, session) = Start(UnsavedChoice.Discard);
        await session.OpenAsync();
        Edit(engine);

        _files.PickToOpen = FileB;
        await session.OpenAsync();

        Assert.Equal(FileB, engine.FilePath);
        Assert.Equal("Howe roof truss", engine.Document.Name);
        Assert.Equal(original, _files.Disk[FileA]);
    }

    [Fact]
    public async Task Save_in_the_question_writes_the_documents_own_file_then_goes_on()
    {
        PutOnDisk(FileA, "warren");
        _files.PickToOpen = FileA;
        var (engine, session) = Start(UnsavedChoice.Save);
        await session.OpenAsync();
        Edit(engine);

        await session.NewAsync();

        Assert.Equal(SupportKind.Pin, DocumentSerializer.Deserialize(_files.Disk[FileA]).Nodes.Single(n => n.Id == 4).Support);
        Assert.Equal(0, _files.SavePickerShown);
        Assert.Empty(engine.Document.Nodes);
        Assert.Null(engine.FilePath);
    }

    [Fact]
    public async Task Save_in_the_question_that_is_cancelled_in_the_picker_keeps_the_work()
    {
        var (engine, session) = Start(UnsavedChoice.Save);
        engine.LoadSample("warren");
        _files.PickToSave = null;

        await session.NewAsync();

        Assert.Equal(1, _files.SavePickerShown);
        Assert.Equal("Warren truss", engine.Document.Name);
        Assert.True(engine.IsDirty);
    }

    [Fact]
    public async Task New_on_a_clean_document_does_not_ask()
    {
        PutOnDisk(FileA, "warren");
        _files.PickToOpen = FileA;
        var (engine, session) = Start();
        await session.OpenAsync();

        await session.NewAsync();

        Assert.Empty(_asked);
        Assert.Empty(engine.Document.Nodes);
    }

    [Fact]
    public async Task Save_without_a_file_asks_where_then_saves_there_without_asking_again()
    {
        var (engine, session) = Start();
        engine.LoadSample("roof");
        _files.PickToSave = FileB;

        await session.SaveAsync(ask: false);
        Edit(engine);
        await session.SaveAsync(ask: false);

        Assert.Equal(1, _files.SavePickerShown);
        Assert.Equal(FileB, engine.FilePath);
        Assert.Equal("roof", engine.Document.Name);
        Assert.False(engine.IsDirty);
        Assert.Contains("Saved to /docs/roof.loadpath", _toasts);
    }

    [Fact]
    public async Task Opening_a_malformed_file_says_why_and_leaves_the_document_alone()
    {
        _files.Disk[FileA] = """{ "version": 1, "name": "Broken", "nodes": [ { "id": 1, "x": 0, "y": 0 }, { "id": 1, "x": 1, "y": 0 } ], "members": [] }""";
        _files.PickToOpen = FileA;
        var (engine, session) = Start();
        engine.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
        var before = DocumentSerializer.Serialize(engine.Document);

        await session.OpenAsync();

        Assert.Contains("Could not open: Node 1 appears twice.", _toasts);
        Assert.Equal(before, DocumentSerializer.Serialize(engine.Document));
        Assert.Null(engine.FilePath);
    }

    [Fact]
    public async Task A_restart_brings_back_unsaved_edits_unsaved_and_attached_to_their_file()
    {
        PutOnDisk(FileA, "warren");
        _files.PickToOpen = FileA;
        var (before, beforeSession) = Start();
        await beforeSession.OpenAsync();
        Edit(before);
        await beforeSession.AutosaveAsync();

        var (after, afterSession) = Start(UnsavedChoice.Cancel);
        Assert.True(await afterSession.RestoreAsync());

        Assert.Equal(FileA, after.FilePath);
        Assert.True(after.IsDirty);
        Assert.Equal(SupportKind.Pin, after.Document.GetNode(4).Support);
        // Recovered edits are still protected: Open asks first.
        _files.PickToOpen = FileA;
        await afterSession.OpenAsync();
        Assert.Single(_asked);
        Assert.Equal(SupportKind.Pin, after.Document.GetNode(4).Support);
    }

    [Fact]
    public async Task A_restart_after_a_sample_replaced_a_file_never_saves_over_that_file()
    {
        // The review's repro: open A, open a sample, restart, Save.
        PutOnDisk(FileA, "warren");
        var original = _files.Disk[FileA];
        _files.PickToOpen = FileA;
        var (before, beforeSession) = Start();
        await beforeSession.OpenAsync();
        before.LoadSample("roof");
        await beforeSession.AutosaveAsync();

        var (after, afterSession) = Start();
        await afterSession.RestoreAsync();
        _files.PickToSave = null;
        await afterSession.SaveAsync(ask: false);

        Assert.Null(after.FilePath);
        Assert.Equal(1, _files.SavePickerShown);
        Assert.Equal(original, _files.Disk[FileA]);
    }

    [Fact]
    public async Task Saving_refreshes_what_the_autosave_records()
    {
        var (before, beforeSession) = Start();
        before.LoadSample("roof");
        _files.PickToSave = FileB;
        await beforeSession.SaveAsync(ask: false);
        await beforeSession.AutosaveAsync();

        var (after, afterSession) = Start();
        await afterSession.RestoreAsync();

        Assert.Equal(FileB, after.FilePath);
        Assert.False(after.IsDirty);
    }

    [Fact]
    public async Task Discarding_on_close_drops_the_autosave_for_good()
    {
        var (engine, session) = Start(UnsavedChoice.Discard);
        engine.LoadSample("warren");
        await session.AutosaveAsync();
        Assert.True(File.Exists(AutosavePath));

        Assert.True(await session.ConfirmDiscardAsync(closing: true));
        await session.AutosaveAsync();

        Assert.False(File.Exists(AutosavePath));
        Assert.False(await Start().Session.RestoreAsync());
    }

    [Fact]
    public async Task A_failing_autosave_says_so_when_it_starts_failing_and_not_on_every_edit()
    {
        var folder = Path.Combine(_folder, "gone");
        var path = Path.Combine(folder, "autosave.loadpath");
        var engine = new EditorEngine();
        engine.ToastRequested += (_, message) => _toasts.Add(message);
        var session = new DocumentSession(engine, _files, path, _ => Task.FromResult(UnsavedChoice.Cancel));
        engine.LoadSample("warren");
        int Failures() => _toasts.Count(t => t.StartsWith("Autosave failed: "));

        await session.AutosaveAsync();
        await session.AutosaveAsync();
        Assert.Equal(1, Failures());

        Directory.CreateDirectory(folder);
        await session.AutosaveAsync();
        Assert.True(File.Exists(path));
        Assert.Equal(1, Failures());

        Directory.Delete(folder, recursive: true);
        await session.AutosaveAsync();
        Assert.Equal(2, Failures());
    }

    [Fact]
    public async Task Discarding_before_open_or_new_keeps_the_autosave()
    {
        var (engine, session) = Start(UnsavedChoice.Discard);
        engine.LoadSample("warren");
        await session.AutosaveAsync();

        Assert.True(await session.ConfirmDiscardAsync());

        Assert.True(File.Exists(AutosavePath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{ not json")]
    [InlineData("""{ "dirty": true, "document": { "version": 1, "nodes": [ { "id": 1 }, { "id": 1 } ], "members": [] } }""")]
    [InlineData("""{ "dirty": false, "document": { "version": 1, "nodes": [], "members": [] } }""")]
    public async Task A_missing_unreadable_malformed_or_empty_autosave_restores_nothing(string content)
    {
        File.WriteAllText(AutosavePath, content);
        var (engine, session) = Start();

        Assert.False(await session.RestoreAsync());
        Assert.Empty(engine.Document.Nodes);
        Assert.False(engine.IsDirty);
    }

    // The net10.0 build has no clipboard to read (Uno's reference assemblies throw), so paste takes the path every host
    // without one takes: the newest .share.txt file in the fallback folder, where Share writes on such a host.

    [Fact]
    public async Task Pasting_a_share_code_replaces_the_document_as_one_undo_step()
    {
        var roof = new StructureDocument();
        SampleStructures.Build("roof").Restore(roof);
        File.WriteAllText(Path.Combine(_folder, "Howe roof truss.share.txt"), ShareCode.Encode(roof));
        var (engine, session) = Start();
        engine.LoadSnapshot(SampleStructures.Build("cantilever"), "Open", "/docs/cantilever.loadpath");

        await session.PasteAsync();

        Assert.Equal("Howe roof truss", engine.Document.Name);
        Assert.True(engine.IsDirty);
        Assert.Null(engine.FilePath);
        Assert.Contains("Pasted Howe roof truss · Ctrl+Z to undo", _toasts);
        engine.History.Undo();
        Assert.Equal("Cantilever", engine.Document.Name);
    }

    [Theory]
    [InlineData("LP1.AAAA", "That share code is damaged or incomplete")]
    [InlineData("""{ "version": 1, "nodes": [ { "id": 2, "x": 0, "y": 0 }, { "id": 2, "x": 1, "y": 0 } ], "members": [] }""", "Node 2 appears twice.")]
    public async Task A_bad_pasted_design_is_refused_and_leaves_the_document_alone(string pasted, string reason)
    {
        File.WriteAllText(Path.Combine(_folder, "pasted.share.txt"), pasted);
        var (engine, session) = Start();
        engine.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
        var before = DocumentSerializer.Serialize(engine.Document);

        await session.PasteAsync();

        Assert.Contains(reason, _toasts);
        Assert.Equal(before, DocumentSerializer.Serialize(engine.Document));
        Assert.False(engine.History.CanUndo);
    }
}
