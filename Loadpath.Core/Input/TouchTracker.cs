using Loadpath.Core.Geometry;

namespace Loadpath.Core.Input;

/// <summary>What the editor should do in answer to a touch contact event.</summary>
public enum TouchSignal
{
    None,
    /// <summary>The finger moved past the slop: deliver the held-back press at <see cref="TouchTracker.PressPoint"/>, then this move.</summary>
    BeginDrag,
    Drag,
    EndDrag,
    /// <summary>The finger lifted without moving: deliver a press and a release at <see cref="TouchTracker.PressPoint"/>.</summary>
    Tap,
    /// <summary>The finger stayed put past <see cref="TouchTracker.LongPressMs"/>: open the context menu at the press point.</summary>
    LongPress,
    /// <summary>A second finger landed: cancel any one-finger gesture and start a pinch with <see cref="TouchTracker.PinchPoints"/>.</summary>
    PinchStart,
    Pinch,
}

/// <summary>
/// Turns touch contacts into editor gestures. One finger acts like the left mouse button, except that its press is held
/// back until the finger moves past the slop (a drag), lifts (a tap) or rests (a long press). A second finger can then
/// still turn the touch into a pinch before any tool has acted, so zooming never drops a stray node. Two fingers pinch
/// and pan; once a pinch has started the contacts are spent until every finger is up.
/// </summary>
public sealed class TouchTracker
{
    public const double SlopPx = 8;
    public const double LongPressMs = 500;

    private enum Phase { Idle, Pending, Dragging, Pinching, Spent }

    private readonly List<(uint Id, Vec2 At)> _contacts = new(4);
    private Phase _phase;
    private uint _primary;
    private double _pressTime;

    /// <summary>Where the first finger landed.</summary>
    public Vec2 PressPoint { get; private set; }
    public int ContactCount => _contacts.Count;
    public bool IsDragging => _phase == Phase.Dragging;
    public bool IsPinching => _phase == Phase.Pinching;
    /// <summary>True while a finger is down and its press has not been resolved yet (tap, drag or long press).</summary>
    public bool IsPending => _phase == Phase.Pending;

    /// <summary>The first two fingers, for the pinch.</summary>
    public (Vec2 A, Vec2 B) PinchPoints => _contacts.Count >= 2 ? (_contacts[0].At, _contacts[1].At) : default;

    public TouchSignal Down(uint id, Vec2 at, double timeMs)
    {
        Set(id, at);
        switch (_phase)
        {
            case Phase.Idle:
                _primary = id;
                PressPoint = at;
                _pressTime = timeMs;
                _phase = Phase.Pending;
                return TouchSignal.None;
            case Phase.Pending:
            case Phase.Dragging:
                _phase = Phase.Pinching;
                return TouchSignal.PinchStart;
            default:
                return TouchSignal.None;
        }
    }

    public TouchSignal Move(uint id, Vec2 at)
    {
        if (!Set(id, at, onlyExisting: true)) return TouchSignal.None;
        switch (_phase)
        {
            case Phase.Pending when id == _primary:
                if ((at - PressPoint).Length < SlopPx) return TouchSignal.None;
                _phase = Phase.Dragging;
                return TouchSignal.BeginDrag;
            case Phase.Dragging when id == _primary:
                return TouchSignal.Drag;
            case Phase.Pinching:
                return TouchSignal.Pinch;
            default:
                return TouchSignal.None;
        }
    }

    public TouchSignal Up(uint id, Vec2 at)
    {
        if (!Remove(id)) return TouchSignal.None;
        var signal = TouchSignal.None;
        switch (_phase)
        {
            case Phase.Pending when id == _primary:
                signal = TouchSignal.Tap;
                break;
            case Phase.Dragging when id == _primary:
                signal = TouchSignal.EndDrag;
                break;
        }
        _phase = _contacts.Count == 0 ? Phase.Idle : Phase.Spent;
        return signal;
    }

    /// <summary>The platform took the contact away (capture lost, cancel): a drag ends, a held press is dropped.</summary>
    public TouchSignal Cancel(uint id)
    {
        if (!Remove(id)) return TouchSignal.None;
        var signal = _phase == Phase.Dragging && id == _primary ? TouchSignal.EndDrag : TouchSignal.None;
        _phase = _contacts.Count == 0 ? Phase.Idle : Phase.Spent;
        return signal;
    }

    /// <summary>Call on a timer while <see cref="IsPending"/>: reports the long press once it is due.</summary>
    public TouchSignal Tick(double timeMs)
    {
        if (_phase != Phase.Pending || timeMs - _pressTime < LongPressMs) return TouchSignal.None;
        _phase = Phase.Spent;
        return TouchSignal.LongPress;
    }

    public void Reset()
    {
        _contacts.Clear();
        _phase = Phase.Idle;
    }

    private bool Set(uint id, Vec2 at, bool onlyExisting = false)
    {
        for (var i = 0; i < _contacts.Count; i++)
        {
            if (_contacts[i].Id != id) continue;
            _contacts[i] = (id, at);
            return true;
        }
        if (onlyExisting) return false;
        _contacts.Add((id, at));
        return true;
    }

    private bool Remove(uint id)
    {
        var i = _contacts.FindIndex(c => c.Id == id);
        if (i < 0) return false;
        _contacts.RemoveAt(i);
        return true;
    }
}
