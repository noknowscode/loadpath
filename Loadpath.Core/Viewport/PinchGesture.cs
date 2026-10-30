using Loadpath.Core.Geometry;

namespace Loadpath.Core.Viewport;

/// <summary>
/// Two-finger pinch and pan. The world point under the fingers' midpoint when the gesture began stays under the
/// midpoint as it moves (pan), and the scale follows the ratio of the finger distances (zoom), clamped to the
/// viewport's range.
/// </summary>
public sealed class PinchGesture
{
    private readonly double _startScale;
    private readonly double _startDistance;
    private readonly Vec2 _anchorWorld;

    public PinchGesture(Viewport viewport, Vec2 a, Vec2 b)
    {
        _startScale = viewport.Scale;
        _startDistance = Math.Max(1, (a - b).Length);
        _anchorWorld = viewport.ToWorld(Midpoint(a, b));
    }

    /// <summary>The scale and offset for the fingers at <paramref name="a"/> and <paramref name="b"/>.</summary>
    public (double Scale, Vec2 Offset) Frame(Vec2 a, Vec2 b)
    {
        var scale = Math.Clamp(_startScale * Math.Max(1, (a - b).Length) / _startDistance, Viewport.MinScale, Viewport.MaxScale);
        var c = Midpoint(a, b);
        return (scale, new Vec2(c.X - _anchorWorld.X * scale, c.Y + _anchorWorld.Y * scale));
    }

    public void Apply(Viewport viewport, Vec2 a, Vec2 b)
    {
        var (scale, offset) = Frame(a, b);
        viewport.Set(scale, offset);
    }

    private static Vec2 Midpoint(Vec2 a, Vec2 b) => new((a.X + b.X) / 2, (a.Y + b.Y) / 2);
}
