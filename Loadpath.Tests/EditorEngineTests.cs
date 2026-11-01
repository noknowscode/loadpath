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
}
