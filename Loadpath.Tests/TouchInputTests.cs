using Loadpath.Core.Geometry;
using Loadpath.Core.Samples;
using Loadpath.Presentation;
using Loadpath.Workspace;
using Loadpath.Workspace.Tools;
using Windows.System;
using Xunit;

namespace Loadpath.Tests;

/// <summary>Fingers on the canvas become the mouse's presses, moves and releases, a pinch on the view, or the menu.</summary>
public class TouchInputTests
{
    private sealed class Canvas : ITouchCanvas
    {
        public List<(string Kind, PointerState State)> Calls { get; } = new();
        public int ViewTakenOver { get; private set; }

        public void Press(PointerState state) => Calls.Add(("press", state));
        public void Move(PointerState state) => Calls.Add(("move", state));
        public void Release(PointerState state) => Calls.Add(("release", state));
        public void OpenContextMenu(PointerState state) => Calls.Add(("menu", state));
        public void TakeOverView() => ViewTakenOver++;
    }

    private readonly EditorEngine _engine = new();
    private readonly Canvas _canvas = new();
    private readonly TestUiThread _ui = new();
    private long _now = 1_000;
    private readonly TouchInput _touch;

    public TouchInputTests()
    {
        _engine.Viewport.Resize(1200, 800);
        _touch = new TouchInput(_engine, _canvas, _ui, () => _now);
    }

    private static readonly VirtualKeyModifiers None = VirtualKeyModifiers.None;

    [Fact]
    public void A_tap_presses_and_releases_where_the_finger_landed()
    {
        _touch.Down(1, new Vec2(100, 100), None);
        Assert.Empty(_canvas.Calls);
        _touch.Up(1, new Vec2(103, 101), None);

        Assert.Equal(new[] { "press", "release" }, _canvas.Calls.Select(c => c.Kind));
        Assert.All(_canvas.Calls, c => Assert.Equal(new Vec2(100, 100), c.State.Screen));
        Assert.True(_canvas.Calls[0].State.IsLeft && _canvas.Calls[0].State.IsTouch);
        Assert.False(_canvas.Calls[1].State.IsLeft);
    }

    [Fact]
    public void A_drag_delivers_the_held_press_then_follows_the_finger()
    {
        _engine.Options.MultiSelect = true;
        _touch.Down(1, new Vec2(100, 100), None);
        _touch.Move(1, new Vec2(100, 160), VirtualKeyModifiers.Shift);
        _touch.Move(1, new Vec2(100, 200), None);
        _touch.Up(1, new Vec2(100, 200), None);

        Assert.Equal(new[] { "press", "move", "move", "release" }, _canvas.Calls.Select(c => c.Kind));
        Assert.Equal(new Vec2(100, 100), _canvas.Calls[0].State.Screen);
        Assert.True(_canvas.Calls[1].State.Shift);
        Assert.True(_canvas.Calls[0].State.Multi);
        Assert.Equal(_engine.Viewport.ToWorld(new Vec2(100, 200)), _canvas.Calls[2].State.World);
    }

    [Fact]
    public void A_second_finger_cancels_a_drag_and_pinches_the_view()
    {
        _engine.LoadSnapshot(SampleStructures.Build("warren"), "Open", null);
        var scale = _engine.Viewport.Scale;
        // A real drag through the engine, so the tool is busy when the second finger lands.
        var node = _engine.Viewport.ToScreen(_engine.Document.GetNode(2).Position);
        _engine.Interaction.PointerPressed(new PointerState(node, _engine.Viewport.ToWorld(node), false, false, false, true, false, false, IsTouch: true));
        _engine.Interaction.PointerMoved(new PointerState(node + new Vec2(0, -40), _engine.Viewport.ToWorld(node + new Vec2(0, -40)), false, false, false, true, false, false, IsTouch: true));
        Assert.True(_engine.Interaction.IsBusy);

        _touch.Down(1, new Vec2(400, 400), None);
        _touch.Down(2, new Vec2(500, 400), None);
        _touch.Move(2, new Vec2(600, 400), None);

        Assert.False(_engine.Interaction.IsBusy);
        Assert.True(_canvas.ViewTakenOver >= 1);
        Assert.True(_engine.Viewport.Scale > scale);
        Assert.Empty(_canvas.Calls);
    }

    [Fact]
    public void A_resting_finger_opens_the_menu_once_the_long_press_has_passed()
    {
        _touch.Down(1, new Vec2(300, 200), None);
        var poll = Assert.Single(_ui.Timers);
        Assert.True(poll.IsRunning);

        _now += 200;
        _ui.Elapse();
        Assert.Empty(_canvas.Calls);
        Assert.True(poll.IsRunning);

        _now += 400;
        _ui.Elapse();

        var (kind, state) = Assert.Single(_canvas.Calls);
        Assert.Equal("menu", kind);
        Assert.Equal(new Vec2(300, 200), state.Screen);
        Assert.True(state.IsTouch);
        Assert.False(poll.IsRunning);
    }

    [Fact]
    public void Lifting_the_finger_stops_the_long_press()
    {
        _touch.Down(1, new Vec2(300, 200), None);
        _touch.Up(1, new Vec2(300, 200), None);

        Assert.False(Assert.Single(_ui.Timers).IsRunning);
        _now += 1_000;
        _ui.Elapse();
        Assert.DoesNotContain(_canvas.Calls, c => c.Kind == "menu");
    }
}
