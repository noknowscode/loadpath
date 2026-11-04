using Loadpath.Core.Editing;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Loadpath.Presentation;
using Loadpath.Services;
using Xunit;

namespace Loadpath.Tests;

/// <summary>Closing with unsaved work is held until the user saves, discards or cancels.</summary>
public sealed class CloseGuardTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "loadpath-close-" + Guid.NewGuid().ToString("N"));
    private readonly EditorEngine _engine = new();
    private readonly ScriptedPrompt _prompt = new(UnsavedChoice.Cancel);
    private readonly CloseGuard _guard;

    public CloseGuardTests()
    {
        Directory.CreateDirectory(_folder);
        var store = new MemorySettingsStore(Path.Combine(_folder, "autosave.loadpath"));
        var session = new DocumentSession(_engine, new NoFiles(), store, _prompt, new TestUiThread(), Logs.For<DocumentSession>());
        _guard = new CloseGuard(_engine, session, Logs.For<CloseGuard>());
        _engine.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private sealed class NoFiles : IDocumentFiles
    {
        public Task<(DocumentSnapshot Snapshot, string Path)?> OpenAsync() => Task.FromResult<(DocumentSnapshot, string)?>(null);
        public Task<string?> SaveAsync(StructureDocument doc, string? existingPath, bool forceAsk) => Task.FromResult<string?>(null);
        public string? FallbackFolder => null;
        public string? LatestInFallbackFolder(string pattern) => null;
    }

    [Fact]
    public void A_clean_document_closes_without_a_question()
    {
        Assert.False(_guard.HoldsClose);
    }

    [Fact]
    public async Task Cancel_keeps_the_window_and_the_work()
    {
        _engine.History.Do(new SetSupportEdit(4, SupportKind.Pin));
        Assert.True(_guard.HoldsClose);

        Assert.False(await _guard.ConfirmAsync());

        Assert.True(_guard.HoldsClose);
        Assert.Equal(new[] { "Warren truss" }, _prompt.Asked);
    }

    [Fact]
    public async Task Discard_lets_the_window_close_and_does_not_ask_again()
    {
        _engine.History.Do(new SetSupportEdit(4, SupportKind.Pin));
        _prompt.Answer = UnsavedChoice.Discard;

        Assert.True(await _guard.ConfirmAsync());

        Assert.False(_guard.HoldsClose);
    }

    [Fact]
    public async Task Save_that_is_cancelled_keeps_the_window()
    {
        _engine.History.Do(new SetSupportEdit(4, SupportKind.Pin));
        _prompt.Answer = UnsavedChoice.Save;

        Assert.False(await _guard.ConfirmAsync());
        Assert.True(_guard.HoldsClose);
    }
}
