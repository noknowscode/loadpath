using Loadpath.Core.Editing;
using Loadpath.Core.Geometry;
using Loadpath.Core.Input;
using Loadpath.Core.Model;
using Loadpath.Core.Viewport;
using Xunit;

namespace Loadpath.Tests;

/// <summary>Touch model: hit radii by pointer type, the one-finger press arbitration and the two-finger pinch.</summary>
public class TouchTests
{
    private static (StructureDocument Doc, Viewport Vp) TwoNodesAndAMember()
    {
        var doc = new StructureDocument();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(0, 0)));
        h.Do(new AddNodeEdit(new Vec2(4, 0)));
        h.Do(new AddMemberEdit(1, 2, Section.Chs48));
        var vp = new Viewport();
        vp.Resize(1000, 800);
        return (doc, vp);
    }

    [Fact]
    public void Touch_radii_reach_a_node_the_mouse_misses()
    {
        var (doc, vp) = TwoNodesAndAMember();
        var near = vp.ToScreen(new Vec2(0, 0)) + new Vec2(0, -18); // 18 px above node 1, off the member
        Assert.True(HitTester.Test(doc, vp, near, HitRadii.Mouse).IsNone);
        var touch = HitTester.Test(doc, vp, near, HitRadii.Touch);
        Assert.Equal(HitKind.Node, touch.Kind);
        Assert.Equal(1, touch.Id);
    }

    [Fact]
    public void Touch_band_selects_a_member_the_mouse_misses()
    {
        var (doc, vp) = TwoNodesAndAMember();
        var beside = vp.ToScreen(new Vec2(2, 0)) + new Vec2(0, 15); // mid-member, 15 px below it
        Assert.True(HitTester.Test(doc, vp, beside, HitRadii.Mouse).IsNone);
        Assert.Equal(HitKind.Member, HitTester.Test(doc, vp, beside, HitRadii.Touch).Kind);
    }

    [Fact]
    public void Mouse_radii_are_unchanged_and_nodes_still_win()
    {
        var (doc, vp) = TwoNodesAndAMember();
        Assert.Equal(new HitRadii(HitTester.NodeRadiusPx, HitTester.LoadHandleRadiusPx, HitTester.MemberTolerancePx), HitRadii.For(touch: false));
        Assert.Equal(HitRadii.Touch, HitRadii.For(touch: true));
        // On the member, 10 px from node 1: touch picks the node (nodes are on top), mouse the member.
        var p = vp.ToScreen(new Vec2(0, 0)) + new Vec2(10, 0);
        Assert.Equal(HitKind.Node, HitTester.Test(doc, vp, p, HitRadii.Touch).Kind);
        Assert.Equal(HitKind.Member, HitTester.Test(doc, vp, p, HitRadii.Mouse).Kind);
        // The default overload keeps the mouse radii.
        Assert.Equal(HitTester.Test(doc, vp, p, HitRadii.Mouse), HitTester.Test(doc, vp, p));
    }

    [Fact]
    public void Tap_is_held_back_until_the_finger_lifts()
    {
        var t = new TouchTracker();
        Assert.Equal(TouchSignal.None, t.Down(1, new Vec2(100, 100), 0));
        Assert.True(t.IsPending);
        Assert.Equal(TouchSignal.None, t.Move(1, new Vec2(103, 102))); // inside the slop
        Assert.Equal(TouchSignal.Tap, t.Up(1, new Vec2(103, 102)));
        Assert.Equal(new Vec2(100, 100), t.PressPoint);
        Assert.Equal(0, t.ContactCount);
    }

    [Fact]
    public void Moving_past_the_slop_begins_a_drag_from_the_press_point()
    {
        var t = new TouchTracker();
        t.Down(7, new Vec2(10, 10), 0);
        Assert.Equal(TouchSignal.BeginDrag, t.Move(7, new Vec2(10, 10 + TouchTracker.SlopPx + 1)));
        Assert.Equal(TouchSignal.Drag, t.Move(7, new Vec2(10, 40)));
        Assert.Equal(TouchSignal.None, t.Tick(5000)); // a drag never turns into a long press
        Assert.Equal(TouchSignal.EndDrag, t.Up(7, new Vec2(10, 40)));
        Assert.Equal(new Vec2(10, 10), t.PressPoint);
    }

    [Fact]
    public void Resting_finger_becomes_a_long_press_once()
    {
        var t = new TouchTracker();
        t.Down(1, new Vec2(50, 50), 1000);
        Assert.Equal(TouchSignal.None, t.Tick(1000 + TouchTracker.LongPressMs - 1));
        Assert.Equal(TouchSignal.LongPress, t.Tick(1000 + TouchTracker.LongPressMs));
        Assert.Equal(TouchSignal.None, t.Tick(9999));
        // The lift after a long press is spent: no tap, no release.
        Assert.Equal(TouchSignal.None, t.Move(1, new Vec2(80, 80)));
        Assert.Equal(TouchSignal.None, t.Up(1, new Vec2(80, 80)));
        Assert.False(t.IsPending);
    }

    [Fact]
    public void Second_finger_turns_a_held_press_into_a_pinch_and_spends_the_rest()
    {
        var t = new TouchTracker();
        t.Down(1, new Vec2(100, 100), 0);
        Assert.Equal(TouchSignal.PinchStart, t.Down(2, new Vec2(200, 100), 10));
        Assert.Equal((new Vec2(100, 100), new Vec2(200, 100)), t.PinchPoints);
        Assert.Equal(TouchSignal.Pinch, t.Move(2, new Vec2(260, 100)));
        // One finger lifts: the other must not resume a tool gesture.
        Assert.Equal(TouchSignal.None, t.Up(2, new Vec2(260, 100)));
        Assert.Equal(TouchSignal.None, t.Move(1, new Vec2(140, 140)));
        Assert.Equal(TouchSignal.None, t.Up(1, new Vec2(140, 140)));
        // All up: the next touch starts fresh.
        t.Down(3, new Vec2(5, 5), 100);
        Assert.Equal(TouchSignal.Tap, t.Up(3, new Vec2(5, 5)));
    }

    [Fact]
    public void Second_finger_during_a_drag_starts_a_pinch_so_the_drag_can_be_cancelled()
    {
        var t = new TouchTracker();
        t.Down(1, new Vec2(0, 0), 0);
        Assert.Equal(TouchSignal.BeginDrag, t.Move(1, new Vec2(30, 0)));
        Assert.Equal(TouchSignal.PinchStart, t.Down(2, new Vec2(100, 0), 20));
        Assert.True(t.IsPinching);
        Assert.Equal(TouchSignal.Pinch, t.Move(1, new Vec2(10, 0)));
    }

    [Fact]
    public void Cancelled_contact_ends_a_drag_but_never_taps()
    {
        var t = new TouchTracker();
        t.Down(1, new Vec2(0, 0), 0);
        Assert.Equal(TouchSignal.None, t.Cancel(1));
        t.Down(1, new Vec2(0, 0), 0);
        t.Move(1, new Vec2(0, 50));
        Assert.Equal(TouchSignal.EndDrag, t.Cancel(1));
        Assert.Equal(0, t.ContactCount);
    }

    [Fact]
    public void Pinch_keeps_the_world_point_under_the_fingers_and_scales_with_their_spread()
    {
        var vp = new Viewport();
        vp.Resize(1000, 800);
        vp.Set(80, new Vec2(100, 500));
        var a = new Vec2(300, 300);
        var b = new Vec2(500, 300);
        var anchor = vp.ToWorld(new Vec2(400, 300));
        var pinch = new PinchGesture(vp, a, b);

        // Fingers spread to twice the distance around the same midpoint: scale doubles, the anchor stays put.
        pinch.Apply(vp, new Vec2(200, 300), new Vec2(600, 300));
        Assert.Equal(160, vp.Scale, 6);
        var at = vp.ToScreen(anchor);
        Assert.Equal(400, at.X, 6);
        Assert.Equal(300, at.Y, 6);

        // Both fingers slide 50 px right and 20 px down at the same spread: a pure pan.
        pinch.Apply(vp, new Vec2(250, 320), new Vec2(650, 320));
        Assert.Equal(160, vp.Scale, 6);
        at = vp.ToScreen(anchor);
        Assert.Equal(450, at.X, 6);
        Assert.Equal(320, at.Y, 6);
    }

    [Fact]
    public void Pinch_clamps_to_the_viewport_range()
    {
        var vp = new Viewport();
        vp.Resize(1000, 800);
        var pinch = new PinchGesture(vp, new Vec2(0, 0), new Vec2(400, 0));
        pinch.Apply(vp, new Vec2(0, 0), new Vec2(100000, 0));
        Assert.Equal(Viewport.MaxScale, vp.Scale);
        pinch.Apply(vp, new Vec2(0, 0), new Vec2(0.0001, 0));
        Assert.Equal(Viewport.MinScale, vp.Scale);
    }
}
