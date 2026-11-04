using Loadpath.Core.Geometry;
using Loadpath.Presentation;
using Xunit;

namespace Loadpath.Tests;

/// <summary>Where the overlays go on the canvas: the floating bar, and the pan that clears a sheet.</summary>
public class CanvasLayoutTests
{
    private static Bounds Box(double x0, double y0, double x1, double y1) => new(new Vec2(x0, y0), new Vec2(x1, y1));
    private static Bounds At(double x, double y) => Box(x, y, x, y);

    /// <summary>A Warren truss on screen: top and bottom chords, diagonals, and load arrows rising from the bottom nodes.</summary>
    private static List<(Vec2 A, Vec2 B)> Truss(double left, double right, double top, double bottom, double arrowTop)
    {
        var d = new List<(Vec2, Vec2)> { (new(left, bottom), new(right, bottom)), (new(left, top), new(right, top)), (new(left, bottom), new(left, top)), (new(right, bottom), new(right, top)) };
        for (var x = left + (right - left) / 6; x < right - 1; x += (right - left) / 6) d.Add((new(x, bottom), new(x, arrowTop)));
        return d;
    }

    // The phone capture that showed the defect: a 390 px canvas, a shallow Warren whose top chord is at y 333 and bottom
    // chord at y 361, load arrows (with their "15 kN" labels) up to y 288, node N3 on the bottom chord selected, and a
    // 282 x 50 bar (44 px targets).
    private static readonly Bounds PhoneCanvas = Box(0, 0, 390, 650);
    private static readonly List<(Vec2 A, Vec2 B)> PhoneTruss = Truss(38, 292, 333, 361, 288);
    private static readonly Vec2 PhoneBar = new(282, 50);

    private static bool Covers(Vec2 at, Vec2 size, IEnumerable<(Vec2 A, Vec2 B)> drawing) =>
        drawing.Any(s => new Bounds(at, at + size).IntersectsSegment(s.A, s.B));

    [Fact]
    public void On_a_phone_the_bar_for_a_bottom_chord_node_sits_above_the_truss_and_its_loads()
    {
        var at = CanvasLayout.FloatingBar(At(123, 361), PhoneTruss, PhoneBar, PhoneCanvas);

        Assert.False(Covers(at, PhoneBar, PhoneTruss));
        Assert.Equal(288 - CanvasLayout.Clearance - PhoneBar.Y, at.Y);
        Assert.InRange(at.X, CanvasLayout.Gutter, PhoneCanvas.Max.X - PhoneBar.X - CanvasLayout.Gutter);
    }

    [Fact]
    public void A_node_with_room_above_it_keeps_the_bar_just_above_it_as_before()
    {
        var canvas = Box(0, 0, 1100, 800);
        var truss = Truss(200, 800, 300, 500, 450);
        var bar = new Vec2(150, 32);

        var at = CanvasLayout.FloatingBar(At(500, 300), truss, bar, canvas);

        Assert.Equal(new Vec2(500 + CanvasLayout.BarSide, 300 - 80), at);
    }

    [Fact]
    public void A_lone_node_below_the_truss_keeps_its_bar_beside_it()
    {
        var canvas = Box(0, 0, 1100, 900);
        var drawing = Truss(380, 980, 390, 490, 430);
        drawing.Add((new(708, 753), new(708, 753)));
        var bar = new Vec2(230, 32);

        var at = CanvasLayout.FloatingBar(At(708, 753), drawing, bar, canvas);

        Assert.Equal(753 - CanvasLayout.BarLift - 32, at.Y);
    }

    [Fact]
    public void A_bottom_node_of_a_deep_truss_puts_the_bar_above_the_whole_truss()
    {
        var canvas = Box(0, 0, 1100, 800);
        var truss = Truss(200, 800, 150, 500, 300);
        var bar = new Vec2(150, 32);

        var at = CanvasLayout.FloatingBar(At(500, 500), truss, bar, canvas);

        Assert.Equal(150 - CanvasLayout.Clearance - 32, at.Y);
        Assert.False(Covers(at, bar, truss));
    }

    [Fact]
    public void With_no_room_above_the_truss_the_bar_goes_below_it()
    {
        var truss = Truss(38, 352, 64, 300, 200);

        var at = CanvasLayout.FloatingBar(At(200, 300), truss, PhoneBar, PhoneCanvas);

        Assert.Equal(300 + CanvasLayout.BarDrop, at.Y);
        Assert.False(Covers(at, PhoneBar, truss));
    }

    [Fact]
    public void A_drawing_that_fills_the_canvas_leaves_the_bar_above_the_selection()
    {
        var truss = Truss(-100, 500, 20, 640, 300);

        var at = CanvasLayout.FloatingBar(At(200, 400), truss, PhoneBar, PhoneCanvas);

        Assert.Equal(400 - CanvasLayout.BarLift - PhoneBar.Y, at.Y);
    }

    [Fact]
    public void Near_the_right_edge_the_bar_flips_to_the_left_of_the_selection()
    {
        var canvas = Box(0, 0, 1100, 800);
        var bar = new Vec2(150, 32);

        var at = CanvasLayout.FloatingBar(At(1050, 300), Truss(500, 1050, 300, 400, 350), bar, canvas);

        Assert.Equal(1050 - CanvasLayout.BarSide - 150, at.X);
    }

    [Fact]
    public void A_side_sheet_covers_the_right_and_a_bottom_sheet_the_bottom_only_while_open()
    {
        Assert.Equal((336.0, 0.0), CanvasLayout.Covered(docked: false, sheetOpen: true, narrow: false, 336, 300));
        Assert.Equal((0.0, 300.0), CanvasLayout.Covered(docked: false, sheetOpen: true, narrow: true, 336, 300));
        Assert.Equal((0.0, 0.0), CanvasLayout.Covered(docked: false, sheetOpen: false, narrow: true, 336, 300));
        Assert.Equal((0.0, 0.0), CanvasLayout.Covered(docked: true, sheetOpen: true, narrow: false, 336, 300));
        Assert.Equal(Box(0, 0, 664, 700), CanvasLayout.Uncovered(1000, 700, (336, 0)));
    }

    [Fact]
    public void The_reveal_pans_as_little_as_needed_and_centres_what_does_not_fit()
    {
        var target = Box(100, 50, 600, 650);

        Assert.Equal(Vec2.Zero, CanvasLayout.RevealShift(Box(200, 100, 300, 200), target));
        Assert.Equal(new Vec2(-150, 0), CanvasLayout.RevealShift(Box(650, 100, 750, 200), target));
        Assert.Equal(new Vec2(50, 0), CanvasLayout.RevealShift(Box(50, 100, 80, 200), target));
        // 800 px wide selection in a 500 px target: centred.
        Assert.Equal(new Vec2(350 - 400, 0), CanvasLayout.RevealShift(Box(0, 100, 800, 200), target));
    }

    [Fact]
    public void The_engine_lists_members_loads_supports_and_nodes_as_the_drawing()
    {
        var engine = new EditorEngine();
        engine.LoadSnapshot(Loadpath.Core.Samples.SampleStructures.Build("warren"), "Open", null);
        var doc = engine.Document;

        var drawing = engine.DrawingOnScreen();

        Assert.Equal(doc.Members.Count + doc.Nodes.Count + doc.Nodes.Count(n => n.HasLoad) + doc.Nodes.Count(n => n.HasSupport), drawing.Count);
        // A downward load's arrow rises above its node.
        var loaded = doc.Nodes.First(n => n.HasLoad);
        var at = engine.Viewport.ToScreen(loaded.Position);
        Assert.Contains(drawing, s => s.A == at && s.B.Y < at.Y - 22);
        Assert.Equal(engine.SelectionOnScreen(), Bounds.Empty);
    }
}
