using Loadpath.Core.Geometry;
using Loadpath.Presentation;
using Xunit;

namespace Loadpath.Tests;

/// <summary>Where the overlays go on the canvas: the floating bar, and the pan that clears a sheet.</summary>
public class CanvasLayoutTests
{
    private static Bounds Box(double x0, double y0, double x1, double y1) => new(new Vec2(x0, y0), new Vec2(x1, y1));
    private static Bounds At(double x, double y) => Box(x, y, x, y);

    [Fact]
    public void The_bar_sits_up_and_right_of_the_selection()
    {
        var at = CanvasLayout.FloatingBar(At(500, 300), new Vec2(150, 32), Box(0, 0, 1100, 800));

        Assert.Equal(new Vec2(500 + CanvasLayout.BarSide, 300 - 80), at);
    }

    [Fact]
    public void Near_the_top_edge_the_bar_drops_below_the_selection()
    {
        var at = CanvasLayout.FloatingBar(At(500, 60), new Vec2(150, 32), Box(0, 0, 1100, 800));

        Assert.Equal(60 + CanvasLayout.BarDrop, at.Y);
    }

    [Fact]
    public void Near_the_right_edge_the_bar_flips_to_the_left_of_the_selection()
    {
        var at = CanvasLayout.FloatingBar(At(1050, 300), new Vec2(150, 32), Box(0, 0, 1100, 800));

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
}
