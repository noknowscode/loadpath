using Loadpath.Core.Editing;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Loadpath.Presentation;
using Xunit;

namespace Loadpath.Tests;

public class EditorEngineTests
{
    [Fact]
    public void A_new_engine_is_empty_clean_and_has_no_file()
    {
        var engine = new EditorEngine();
        Assert.Empty(engine.Document.Nodes);
        Assert.False(engine.IsDirty);
        Assert.Null(engine.FilePath);
        Assert.False(engine.History.CanUndo);
    }

    [Fact]
    public void Opening_a_sample_over_work_is_one_undo_step_and_leaves_a_new_unsaved_document()
    {
        var engine = new EditorEngine();
        engine.LoadSnapshot(SampleStructures.Build("warren"), "Open", "/docs/bridge.loadpath");
        engine.History.Do(new SetSupportEdit(4, SupportKind.Pin));
        string? toast = null;
        engine.ToastRequested += (_, message) => toast = message;

        engine.LoadSample("roof");

        Assert.Equal("Howe roof truss", engine.Document.Name);
        Assert.Null(engine.FilePath);
        Assert.True(engine.IsDirty);
        Assert.Equal("Howe roof truss · Ctrl+Z restores your previous structure", toast);

        engine.History.Undo();
        Assert.Equal("Warren truss", engine.Document.Name);
        Assert.Equal(SupportKind.Pin, engine.Document.GetNode(4).Support);
        engine.History.Undo();
        Assert.Equal(SupportKind.None, engine.Document.GetNode(4).Support);
    }

    [Fact]
    public void Opening_a_sample_on_an_empty_canvas_says_nothing_about_undo()
    {
        var engine = new EditorEngine();
        string? toast = null;
        engine.ToastRequested += (_, message) => toast = message;

        engine.LoadSample("cantilever");

        Assert.NotEmpty(engine.Document.Nodes);
        Assert.Null(toast);
    }
}
