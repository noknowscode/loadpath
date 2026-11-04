using Loadpath.Core.Samples;
using Loadpath.Presentation;
using Loadpath.Workspace;
using Xunit;

namespace Loadpath.Tests;

/// <summary>The frame the canvas draws follows the engine: positions, the selection and what it dims, and the layers.</summary>
public class RenderSnapshotBuilderTests
{
    private static EditorEngine Warren()
    {
        var e = new EditorEngine();
        e.Viewport.Resize(1200, 800);
        e.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
        return e;
    }

    [Fact]
    public void Every_element_is_drawn_where_the_viewport_puts_it()
    {
        var e = Warren();

        var frame = RenderSnapshotBuilder.Build(e, 1200, 800, railOverlay: true);

        Assert.Equal(e.Document.Nodes.Count, frame.Nodes.Length);
        Assert.Equal(e.Document.Members.Count, frame.Members.Length);
        var n2 = e.Viewport.ToScreen(e.Document.GetNode(2).Position);
        Assert.Equal(new SkiaSharp.SKPoint((float)n2.X, (float)n2.Y), frame.Nodes.Single(n => n.ElementId == 2).Screen);
        Assert.Equal("Warren truss", frame.DocumentName);
        Assert.True(frame.RailOverlay);
        Assert.False(frame.AnySelected);
    }

    [Fact]
    public void One_selected_member_dims_everything_but_itself_and_its_ends()
    {
        var e = Warren();
        var member = e.Document.GetMember(3);
        e.Selection.Set(member.Ref);

        var frame = RenderSnapshotBuilder.Build(e, 1200, 800, railOverlay: false);

        Assert.All(frame.Members, m => Assert.Equal(m.ElementId != 3, m.Dimmed));
        Assert.True(frame.Members.Single(m => m.ElementId == 3).Selected);
        Assert.All(frame.Nodes, n => Assert.Equal(n.ElementId != member.StartNodeId && n.ElementId != member.EndNodeId, n.Dimmed));
    }

    [Fact]
    public void Reduce_noise_hides_the_layers_it_does_not_keep()
    {
        var e = Warren();
        Assert.True(RenderSnapshotBuilder.Build(e, 1200, 800, false).ShowGrid);

        e.Options.ReduceNoise = true;

        var frame = RenderSnapshotBuilder.Build(e, 1200, 800, false);
        Assert.False(frame.ShowGrid);
        Assert.True(frame.ShowSupports);
    }
}
