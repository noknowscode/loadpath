using Loadpath.Core.Editing;
using Loadpath.Core.Geometry;
using Loadpath.Core.Model;
using Loadpath.Core.Viewport;
using Xunit;

namespace Loadpath.Tests;

public class GeometryTests
{
    [Fact]
    public void ZoomAt_keeps_the_anchor_world_point_fixed()
    {
        var vp = new Viewport();
        vp.Resize(1000, 800);
        var anchor = new Vec2(300, 200);
        var before = vp.ToWorld(anchor);
        vp.ZoomAt(anchor, vp.Scale * 1.7);
        var after = vp.ToWorld(anchor);
        Assert.InRange((before - after).Length, 0, 1e-9);
    }

    [Fact]
    public void ComputeFit_centers_bounds()
    {
        var vp = new Viewport();
        vp.Resize(1000, 800);
        var (scale, offset) = vp.ComputeFit(new Bounds(new Vec2(0, 0), new Vec2(12, 2)));
        vp.Set(scale, offset);
        var c = vp.ToScreen(new Vec2(6, 1));
        Assert.InRange(c.X, 499, 501);
        Assert.InRange(c.Y, 399, 401);
        Assert.True(vp.ToScreen(new Vec2(0, 0)).X > 0);
    }

    [Fact]
    public void Hit_test_prefers_nodes_then_members()
    {
        var doc = new StructureDocument();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(0, 0)));
        h.Do(new AddNodeEdit(new Vec2(2, 0)));
        h.Do(new AddMemberEdit(1, 2, Section.Chs48));
        var vp = new Viewport();
        vp.Resize(1000, 800);
        var onNode = vp.ToScreen(new Vec2(0, 0)) + new Vec2(4, 3);
        Assert.Equal(HitKind.Node, HitTester.Test(doc, vp, onNode).Kind);
        var onMember = vp.ToScreen(new Vec2(1, 0)) + new Vec2(0, 3);
        Assert.Equal(HitKind.Member, HitTester.Test(doc, vp, onMember).Kind);
        var far = vp.ToScreen(new Vec2(1, 1));
        Assert.Equal(HitKind.None, HitTester.Test(doc, vp, far).Kind);
    }

    [Fact]
    public void Snap_yields_guides_and_grid()
    {
        var doc = new StructureDocument();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(0, 0)));
        h.Do(new AddNodeEdit(new Vec2(3, 1.5)));
        var vp = new Viewport();
        vp.Resize(1000, 800);
        var snap = new SnapEngine { GridStep = 0.5 };
        // Near x = 3 (guide) and y ≈ 0.7 (grid → 0.5).
        var r = snap.Snap(doc, vp, new Vec2(3.02, 0.7), allowNodeSnap: false);
        Assert.Equal(new Vec2(3, 0.5), r.Point);
        Assert.Contains(r.Guides, g => g.Axis == GuideAxis.Vertical && g.AnchorNodeId == 2);
        // Close to node 1: node snap when allowed.
        var n = snap.Snap(doc, vp, new Vec2(0.05, 0.02), allowNodeSnap: true);
        Assert.Equal(1, n.SnappedNodeId);
    }

    [Fact]
    public void A_snapped_point_on_a_node_names_that_node_unless_it_is_excluded()
    {
        var doc = new StructureDocument();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(0, 0)));
        var vp = new Viewport();
        vp.Resize(1000, 800);
        var snap = new SnapEngine { GridStep = 0.5 };
        // 0.2 m away is well outside the node tolerance; the grid rounds it onto the node.
        var r = snap.Snap(doc, vp, new Vec2(-0.2, 0.1), allowNodeSnap: false);

        Assert.Equal(new Vec2(0, 0), r.Point);
        Assert.Equal(1, r.SnappedNodeId);
        Assert.Null(snap.Snap(doc, vp, new Vec2(-0.2, 0.1), allowNodeSnap: false, exclude: new HashSet<int> { 1 }).SnappedNodeId);
        Assert.Null(snap.Snap(doc, vp, new Vec2(-0.4, 0.1), allowNodeSnap: false).SnappedNodeId);
    }

    [Fact]
    public void Marquee_contained_vs_touching()
    {
        var doc = new StructureDocument();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(0, 0)));
        h.Do(new AddNodeEdit(new Vec2(4, 0)));
        h.Do(new AddMemberEdit(1, 2, Section.Chs48));
        var vp = new Viewport();
        vp.Resize(1000, 800);
        var rect = Bounds.FromPoints(vp.ToScreen(new Vec2(-0.5, 0.5)), vp.ToScreen(new Vec2(2, -0.5)));
        var contained = HitTester.Marquee(doc, vp, rect, contained: true).ToList();
        Assert.Contains(ElementRef.Node(1), contained);
        Assert.DoesNotContain(ElementRef.Member(1), contained);
        var touching = HitTester.Marquee(doc, vp, rect, contained: false).ToList();
        Assert.Contains(ElementRef.Member(1), touching);
    }
}
