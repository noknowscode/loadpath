using Loadpath.Core.Geometry;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Loadpath.Presentation;
using Loadpath.Workspace.Tools;
using Xunit;

namespace Loadpath.Tests;

/// <summary>A click that the grid and the guides put on a node lands on that node: the tools never stack a second node on it.</summary>
public class ToolPlacementTests
{
    // Warren sample: N1 pin at (0, 0) with members to the right and up to the right; N4 at (6, 0). At 100% a metre is
    // 80 px, so 15 px left of N1 is outside the 10 px node snap and the hit radius, and the 0.5 m grid rounds it onto N1.
    private static readonly Vec2 LeftOfN1 = new(-15, 0);

    private static EditorEngine Warren()
    {
        var e = new EditorEngine();
        e.Viewport.Resize(1200, 800);
        e.Viewport.Set(Core.Viewport.Viewport.BaseScale, new Vec2(100, 500));
        e.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
        return e;
    }

    private static PointerState At(EditorEngine e, Vec2 screen) =>
        new(screen, e.Viewport.ToWorld(screen), Shift: false, Alt: false, Ctrl: false, IsLeft: true, IsMiddle: false, IsRight: false);

    private static Vec2 ScreenOf(EditorEngine e, int nodeId) => e.Viewport.ToScreen(e.Document.GetNode(nodeId).Position);

    private static void Click(EditorEngine e, Vec2 screen)
    {
        e.Interaction.PointerPressed(At(e, screen));
        e.Interaction.PointerReleased(At(e, screen) with { IsLeft = false });
    }

    [Fact]
    public void The_node_tool_selects_the_node_a_snapped_click_lands_on()
    {
        var e = Warren();
        var nodes = e.Document.Nodes.Count;
        e.Interaction.SetTool(ToolKind.Node);

        Click(e, ScreenOf(e, 1) + LeftOfN1);

        Assert.Equal(nodes, e.Document.Nodes.Count);
        Assert.Equal(ElementRef.Node(1), e.Selection.Single);
        Assert.False(e.History.CanUndo);
    }

    [Fact]
    public void The_node_tool_still_places_a_node_on_an_empty_grid_point()
    {
        var e = Warren();
        var nodes = e.Document.Nodes.Count;
        e.Interaction.SetTool(ToolKind.Node);

        Click(e, ScreenOf(e, 1) + new Vec2(-40, 0));

        Assert.Equal(nodes + 1, e.Document.Nodes.Count);
        Assert.Equal(new Vec2(-0.5, 0), e.Document.Nodes[^1].Position);
    }

    [Fact]
    public void The_member_tool_connects_to_the_node_a_snapped_click_lands_on()
    {
        var e = Warren();
        var nodes = e.Document.Nodes.Count;
        e.Interaction.SetTool(ToolKind.Member);

        Click(e, ScreenOf(e, 4) + new Vec2(0, 60));
        var start = e.Selection.Single!.Value.Id;
        Click(e, ScreenOf(e, 1) + LeftOfN1);

        // The chain started on a new node below N4 and ended on N1 itself.
        Assert.Equal(nodes + 1, e.Document.Nodes.Count);
        Assert.NotNull(e.Document.FindMemberBetween(start, 1));
    }

    [Fact]
    public void The_canvas_menu_selects_a_node_already_at_the_point()
    {
        var e = Warren();
        var nodes = e.Document.Nodes.Count;

        e.AddNodeAt(new Vec2(0, 0));

        Assert.Equal(nodes, e.Document.Nodes.Count);
        Assert.Equal(ElementRef.Node(1), e.Selection.Single);
    }
}
