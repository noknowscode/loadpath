using Loadpath.Core.Geometry;
using Loadpath.Core.Samples;
using Loadpath.Presentation;
using Loadpath.Workspace;
using Xunit;

namespace Loadpath.Tests;

/// <summary>A fitted view keeps the structure framed through resizes and layout changes until the user moves it.</summary>
public class ViewFittingTests
{
    private readonly EditorEngine _engine = new();
    private SheetInsets _insets = new((96, 48, 32, 120), (0, 0));
    private readonly ViewFitting _fitting;

    public ViewFittingTests()
    {
        _engine.Viewport.Resize(1200, 800);
        _engine.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
        _fitting = new ViewFitting(_engine, new ViewportAnimation(_engine.Viewport), () => _insets);
    }

    private (double Scale, Vec2 Offset) View => (_engine.Viewport.Scale, _engine.Viewport.Offset);

    private (double Scale, Vec2 Offset) SheetFit(double width, double height) =>
        CanvasLayout.SheetFit(_engine.Document.GetBounds(), width, height, _insets.Fit, _insets.Covered);

    /// <summary>The 12 m Warren at 40 px/m, its left end <paramref name="left"/> px from the canvas edge.</summary>
    private void MoveViewTo(double left) => _engine.Viewport.Set(40, new Vec2(left, 500));

    [Fact]
    public void A_fitted_view_refits_on_every_resize_until_it_is_released()
    {
        _fitting.Fit();

        _fitting.Resize(1000, 700);
        Assert.Equal(SheetFit(1000, 700), View);

        _fitting.Release();
        var released = View;
        _fitting.Resize(1100, 800);

        Assert.False(_fitting.IsFitted);
        Assert.Equal(released, View);
    }

    [Fact]
    public void New_insets_reframe_a_fitted_view_and_leave_a_moved_one()
    {
        _fitting.Fit();
        _fitting.Resize(700, 800);
        _insets = new SheetInsets((96, 48, 32, 120), (0, 300));

        _fitting.LayoutChanged();

        Assert.Equal(SheetFit(700, 800), View);

        _fitting.Release();
        MoveViewTo(100);
        _insets = new SheetInsets((96, 48, 32, 120), (0, 0));
        _fitting.LayoutChanged();

        Assert.Equal((40.0, new Vec2(100, 500)), View);
    }
}
