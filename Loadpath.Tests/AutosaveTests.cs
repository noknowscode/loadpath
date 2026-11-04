using Loadpath.Core.Editing;
using Loadpath.Core.Geometry;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Loadpath.Core.Serialization;
using Loadpath.Presentation;
using Xunit;

namespace Loadpath.Tests;

public class AutosaveTests
{
    private const string FileA = "/home/me/Documents/Loadpath/bridge.loadpath";

    private static EditorEngine Restored(EditorEngine before)
    {
        var json = Autosave.Serialize(before.Document, before.FilePath, before.IsDirty);
        var after = new EditorEngine();
        after.RestoreSession(Autosave.Deserialize(json));
        return after;
    }

    [Fact]
    public void Autosave_round_trips_the_document_its_file_and_its_dirty_flag()
    {
        var doc = new StructureDocument();
        SampleStructures.Build("roof").Restore(doc);

        var back = Autosave.Deserialize(Autosave.Serialize(doc, FileA, dirty: true));

        Assert.Equal(FileA, back.FilePath);
        Assert.True(back.Dirty);
        Assert.Equal(DocumentSnapshot.Capture(doc).Nodes, back.Snapshot.Nodes);
        Assert.Equal(DocumentSnapshot.Capture(doc).Members, back.Snapshot.Members);
    }

    [Fact]
    public void An_older_autosave_holding_only_the_document_restores_unsaved_with_no_file()
    {
        var doc = new StructureDocument();
        SampleStructures.Build("warren").Restore(doc);

        var back = Autosave.Deserialize(DocumentSerializer.Serialize(doc));

        Assert.Null(back.FilePath);
        Assert.True(back.Dirty);
        Assert.Equal(doc.Nodes.Count, back.Snapshot.Nodes.Count);
    }

    [Fact]
    public void Unsaved_edits_to_an_opened_file_restore_unsaved_and_attached_to_that_file()
    {
        var before = new EditorEngine();
        before.LoadSnapshot(SampleStructures.Build("warren"), "Open", FileA);
        before.History.Do(new SetLoadEdit(before.Document.Nodes[2].Id, new Vec2(0, -25)));

        var after = Restored(before);

        Assert.Equal(FileA, after.FilePath);
        Assert.True(after.IsDirty);
        Assert.Equal(new Vec2(0, -25), after.Document.Nodes[2].Load);
    }

    [Fact]
    public void A_saved_file_restores_clean_and_attached_to_that_file()
    {
        var before = new EditorEngine();
        before.LoadSnapshot(SampleStructures.Build("warren"), "Open", FileA);

        var after = Restored(before);

        Assert.Equal(FileA, after.FilePath);
        Assert.False(after.IsDirty);
    }

    [Fact]
    public void A_sample_opened_over_a_file_restores_with_no_file_so_save_cannot_overwrite_it()
    {
        // The review's repro: open file A, open a sample, restart. The sample used to come back attached to A.
        var before = new EditorEngine();
        before.LoadSnapshot(SampleStructures.Build("warren"), "Open", FileA);
        before.LoadSample("roof");

        var after = Restored(before);

        Assert.Null(after.FilePath);
        Assert.Equal("Howe roof truss", after.Document.Name);
    }

    [Fact]
    public void A_restored_session_starts_a_fresh_history()
    {
        var before = new EditorEngine();
        before.LoadSnapshot(SampleStructures.Build("warren"), "Open", FileA);
        before.History.Do(new SetSupportEdit(before.Document.Nodes[0].Id, SupportKind.None));

        var after = Restored(before);

        Assert.False(after.History.CanUndo);
        Assert.True(after.IsDirty);
    }
}
