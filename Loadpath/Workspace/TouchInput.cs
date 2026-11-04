using Loadpath.Core.Input;
using Loadpath.Core.Viewport;
using Loadpath.Presentation;
using Loadpath.Workspace.Tools;
using Windows.System;

namespace Loadpath.Workspace;

/// <summary>What touch drives on the canvas: the presses, moves and releases a mouse makes, the context menu, and the view.</summary>
internal interface ITouchCanvas
{
    /// <summary>A press for the tools, with the canvas's double-tap detection.</summary>
    void Press(PointerState state);
    void Move(PointerState state);
    void Release(PointerState state);
    void OpenContextMenu(PointerState state);
    /// <summary>Two fingers move the view: it stops easing and is no longer kept fitted.</summary>
    void TakeOverView();
}

/// <summary>
/// Touch on the canvas. One finger edits like the left mouse button, but its press is held back until it moves (a drag),
/// lifts (a tap) or rests (a long press, the context menu), so a second finger can turn the touch into a pinch-zoom and
/// pan before any tool acts. <see cref="TouchTracker"/> decides which; this class turns its signals into the canvas's
/// presses, moves and releases, the pinch on the viewport, and the menu. The long press is polled every 60 ms while a
/// press is held back, and only then.
/// </summary>
internal sealed class TouchInput
{
    private static readonly TimeSpan LongPressPoll = TimeSpan.FromMilliseconds(60);

    private readonly EditorEngine _editor;
    private readonly ITouchCanvas _canvas;
    private readonly Func<long> _clockMs;
    private readonly TouchTracker _touch = new();
    private readonly IUiTimer _longPress;
    private bool _polling;
    private PinchGesture? _pinch;

    /// <param name="clockMs">Milliseconds for the long press; the tests hand in their own clock.</param>
    public TouchInput(EditorEngine editor, ITouchCanvas canvas, IUiThread ui, Func<long>? clockMs = null)
    {
        _editor = editor;
        _canvas = canvas;
        _clockMs = clockMs ?? (() => Environment.TickCount64);
        _longPress = ui.CreateTimer(LongPressPoll, OnLongPressPoll);
    }

    public void Down(uint pointer, Vec2 at, VirtualKeyModifiers keys) => On(_touch.Down(pointer, at, _clockMs()), at, keys);

    public void Move(uint pointer, Vec2 at, VirtualKeyModifiers keys) => On(_touch.Move(pointer, at), at, keys);

    public void Up(uint pointer, Vec2 at, VirtualKeyModifiers keys) => On(_touch.Up(pointer, at), at, keys);

    /// <summary>The platform took the finger away (capture lost, cancelled): a drag ends, a held press never taps.</summary>
    public void Cancel(uint pointer, Vec2 at, VirtualKeyModifiers keys) => On(_touch.Cancel(pointer), at, keys);

    private PointerState At(Vec2 screen, bool left, VirtualKeyModifiers keys) => new(
        screen, _editor.Viewport.ToWorld(screen),
        keys.HasFlag(VirtualKeyModifiers.Shift), keys.HasFlag(VirtualKeyModifiers.Menu), keys.HasFlag(VirtualKeyModifiers.Control),
        left, IsMiddle: false, IsRight: false,
        Multi: _editor.Options.MultiSelect, Free: _editor.Options.FreePlacement, IsTouch: true);

    private void On(TouchSignal signal, Vec2 at, VirtualKeyModifiers keys)
    {
        switch (signal)
        {
            case TouchSignal.BeginDrag:
                _canvas.Press(At(_touch.PressPoint, left: true, keys));
                _canvas.Move(At(at, left: true, keys));
                break;
            case TouchSignal.Drag:
                _canvas.Move(At(at, left: true, keys));
                break;
            case TouchSignal.EndDrag:
                _canvas.Release(At(at, left: false, keys));
                break;
            case TouchSignal.Tap:
                var tap = At(_touch.PressPoint, left: true, keys);
                _canvas.Press(tap);
                _canvas.Release(tap with { IsLeft = false });
                break;
            case TouchSignal.PinchStart:
                // A held press never reached a tool; a drag in progress is cancelled (undone) before the view moves.
                if (_editor.Interaction.IsBusy) _editor.Interaction.CancelTool();
                _canvas.TakeOverView();
                var (a, b) = _touch.PinchPoints;
                _pinch = new PinchGesture(_editor.Viewport, a, b);
                break;
            case TouchSignal.Pinch:
                if (_pinch is not null)
                {
                    var (p1, p2) = _touch.PinchPoints;
                    _canvas.TakeOverView();
                    _pinch.Apply(_editor.Viewport, p1, p2);
                }
                break;
            case TouchSignal.LongPress:
                _canvas.OpenContextMenu(At(_touch.PressPoint, left: false, keys));
                break;
        }
        if (!_touch.IsPinching) _pinch = null;
        PollLongPress();
    }

    /// <summary>Polls only while a press is held back; the long press fires from here, with no pointer event to carry it.</summary>
    private void PollLongPress()
    {
        if (!_touch.IsPending)
        {
            _longPress.Stop();
            _polling = false;
            return;
        }
        if (_polling) return;
        _polling = true;
        _longPress.Restart();
    }

    private void OnLongPressPoll()
    {
        _polling = false;
        if (_touch.Tick(_clockMs()) == TouchSignal.LongPress) _canvas.OpenContextMenu(At(_touch.PressPoint, left: false, VirtualKeyModifiers.None));
        else PollLongPress();
    }
}
