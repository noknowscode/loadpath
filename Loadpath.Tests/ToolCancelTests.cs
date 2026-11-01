using Loadpath.Core.Editing;
using Loadpath.Core.Geometry;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Loadpath.Presentation;
using Loadpath.Workspace.Tools;
using Windows.System;
using Xunit;

namespace Loadpath.Tests;

/// <summary>Esc, or a second finger, ends a drag by undoing what that drag did, and nothing before it.</summary>
public class ToolCancelTests
{
    // Warren sample: N1 pin at (0, 0), N2 at (2, 0) carries 15 kN down, N7 roller at (12, 0).
    private const int Loaded = 2;

    private static EditorEngine Warren()
    {
        var e = new EditorEngine();
        e.Viewport.Resize(1200, 800);
        e.Viewport.Set(Core.Viewport.Viewport.BaseScale, new Vec2(100, 500));
        e.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
        // The earlier edit a cancelled drag must leave alone.
        e.History.Do(new SetSupportEdit(4, SupportKind.Pin));
        return e;
    }

    private static PointerState At(EditorEngine e, Vec2 screen, bool left = true) =>
        new(screen, e.Viewport.ToWorld(screen), Shift: false, Alt: false, Ctrl: false, IsLeft: left, IsMiddle: false, IsRight: false);

    private static Vec2 ScreenOf(EditorEngine e, int nodeId) => e.Viewport.ToScreen(e.Document.GetNode(nodeId).Position);

    private static void AssertEarlierEditKept(EditorEngine e)
    {
        Assert.Equal(SupportKind.Pin, e.Document.GetNode(4).Support);
        Assert.Equal("Set pin", e.History.UndoLabel);
    }

    [Fact]
    public void Esc_after_pressing_a_load_handle_undoes_nothing()
    {
        var e = Warren();
        var handle = HitTester.LoadHandleScreen(e.Document.GetNode(Loaded), e.Viewport);

        e.Interaction.PointerPressed(At(e, handle));
        Assert.True(e.Interaction.IsBusy);
        Assert.True(e.Interaction.KeyDown(VirtualKey.Escape));

        Assert.False(e.Interaction.IsBusy);
        Assert.Equal(new Vec2(0, -15), e.Document.GetNode(Loaded).Load);
        AssertEarlierEditKept(e);
    }

    [Fact]
    public void Esc_after_a_node_drag_that_snapping_kept_in_place_undoes_nothing()
    {
        var e = Warren();
        var start = ScreenOf(e, Loaded);

        e.Interaction.PointerPressed(At(e, start));
        // Past the 4 px drag threshold, well inside half a 0.5 m grid step (20 px at 100%): the node snaps back.
        e.Interaction.PointerMoved(At(e, start + new Vec2(6, 0)));
        Assert.True(e.Interaction.IsBusy);
        e.Interaction.KeyDown(VirtualKey.Escape);

        Assert.Equal(new Vec2(2, 0), e.Document.GetNode(Loaded).Position);
        AssertEarlierEditKept(e);
    }

    [Fact]
    public void A_second_finger_during_a_drag_that_moved_nothing_undoes_nothing()
    {
        // A pinch starting mid-drag cancels the tool (WorkspaceView, TouchSignal.PinchStart).
        var e = Warren();
        var start = ScreenOf(e, Loaded);

        e.Interaction.PointerPressed(At(e, start));
        e.Interaction.PointerMoved(At(e, start + new Vec2(0, -6)));
        e.Interaction.CancelTool();

        Assert.Equal(new Vec2(2, 0), e.Document.GetNode(Loaded).Position);
        AssertEarlierEditKept(e);
    }

    [Fact]
    public void Esc_after_a_real_node_drag_puts_the_node_back_and_keeps_the_earlier_edit()
    {
        var e = Warren();
        var start = ScreenOf(e, Loaded);

        e.Interaction.PointerPressed(At(e, start));
        e.Interaction.PointerMoved(At(e, start + new Vec2(40, 0)));
        e.Interaction.PointerMoved(At(e, start + new Vec2(80, 0)));
        Assert.Equal(new Vec2(3, 0), e.Document.GetNode(Loaded).Position);
        e.Interaction.KeyDown(VirtualKey.Escape);

        Assert.Equal(new Vec2(2, 0), e.Document.GetNode(Loaded).Position);
        AssertEarlierEditKept(e);
    }

    [Fact]
    public void Esc_after_a_real_load_handle_drag_restores_the_load_and_keeps_the_earlier_edit()
    {
        var e = Warren();
        var handle = HitTester.LoadHandleScreen(e.Document.GetNode(Loaded), e.Viewport);

        e.Interaction.PointerPressed(At(e, handle));
        e.Interaction.PointerMoved(At(e, handle + new Vec2(0, -40)));
        Assert.NotEqual(new Vec2(0, -15), e.Document.GetNode(Loaded).Load);
        e.Interaction.KeyDown(VirtualKey.Escape);

        Assert.Equal(new Vec2(0, -15), e.Document.GetNode(Loaded).Load);
        AssertEarlierEditKept(e);
    }

    [Fact]
    public void Esc_after_a_load_tool_drag_that_set_no_load_undoes_nothing()
    {
        var e = Warren();
        e.Interaction.SetTool(ToolKind.Load);
        var node = ScreenOf(e, Loaded);

        // Past the 6 px drag threshold, but the tail stays within 6 px of the node, which sets no load.
        e.Interaction.PointerPressed(At(e, node + new Vec2(4, 0)));
        e.Interaction.PointerMoved(At(e, node + new Vec2(-3, 0)));
        Assert.True(e.Interaction.IsBusy);
        e.Interaction.KeyDown(VirtualKey.Escape);

        Assert.Equal(new Vec2(0, -15), e.Document.GetNode(Loaded).Load);
        AssertEarlierEditKept(e);
    }

    [Fact]
    public void Esc_after_a_load_tool_drag_restores_the_load_and_keeps_the_earlier_edit()
    {
        var e = Warren();
        e.Interaction.SetTool(ToolKind.Load);
        var node = ScreenOf(e, Loaded);

        e.Interaction.PointerPressed(At(e, node));
        e.Interaction.PointerMoved(At(e, node + new Vec2(-80, 0)));
        Assert.True(e.Document.GetNode(Loaded).Load.X > 0);
        e.Interaction.KeyDown(VirtualKey.Escape);

        Assert.Equal(new Vec2(0, -15), e.Document.GetNode(Loaded).Load);
        AssertEarlierEditKept(e);
    }
}
