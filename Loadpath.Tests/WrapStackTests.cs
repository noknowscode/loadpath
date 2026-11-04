using Loadpath.Controls;
using Loadpath.Core.Geometry;
using Xunit;

namespace Loadpath.Tests;

/// <summary>The tool rail's panel: a stack that wraps into a new column when the canvas is shorter than the rail.</summary>
public class WrapStackTests
{
    // The floating rail: five 40 px tools, a 9 px rule, two more tools, 2 px apart.
    private static readonly Vec2[] Rail =
    [
        new(40, 40), new(40, 40), new(40, 40), new(40, 40), new(40, 40), new(8, 9), new(40, 40), new(40, 40),
    ];

    [Fact]
    public void With_room_it_is_one_column()
    {
        var (slots, size) = WrapStack.Layout(Rail, 400, 2, vertical: true);

        Assert.Equal(new Vec2(40, 7 * 40 + 9 + 7 * 2), size);
        Assert.All(slots, s => Assert.Equal(0, s.X));
        Assert.Equal(new WrapStack.Slot(0, 5 * 42, 40, 9), slots[5]);
    }

    [Fact]
    public void A_landscape_phone_canvas_wraps_the_last_tools_into_a_second_column()
    {
        // 844 x 390: the canvas row is 294 px; the rail keeps 24 px top and bottom.
        var (slots, size) = WrapStack.Layout(Rail, 294 - 48, 2, vertical: true);

        Assert.Equal(0, slots[5].X);
        Assert.Equal(new WrapStack.Slot(42, 0, 40, 40), slots[6]);
        Assert.Equal(new WrapStack.Slot(42, 42, 40, 40), slots[7]);
        Assert.Equal(new Vec2(82, 5 * 42 + 9), size);
        Assert.All(slots, s => Assert.True(s.Y + s.Height <= 246));
    }

    [Fact]
    public void A_child_longer_than_the_limit_still_gets_a_line_of_its_own()
    {
        var (slots, size) = WrapStack.Layout(new Vec2[] { new(40, 100), new(40, 40) }, 60, 2, vertical: true);

        Assert.Equal(new WrapStack.Slot(0, 0, 40, 100), slots[0]);
        Assert.Equal(new WrapStack.Slot(42, 0, 40, 40), slots[1]);
        Assert.Equal(new Vec2(82, 100), size);
    }

    [Fact]
    public void Horizontal_with_unlimited_width_is_the_phone_strip()
    {
        var (slots, size) = WrapStack.Layout(new Vec2[] { new(44, 44), new(1, 28), new(44, 44) }, double.PositiveInfinity, 2, vertical: false);

        Assert.Equal(new WrapStack.Slot(46, 0, 1, 44), slots[1]);
        Assert.Equal(new Vec2(44 + 1 + 44 + 4, 44), size);
    }

    [Fact]
    public void An_empty_panel_takes_no_room()
    {
        var (slots, size) = WrapStack.Layout(Array.Empty<Vec2>(), 100, 2, vertical: true);

        Assert.Empty(slots);
        Assert.Equal(Vec2.Zero, size);
    }
}
