namespace Loadpath.Workspace;

/// <summary>
/// Eases the viewport to a new scale and offset (fit, wheel zoom): a 16 ms timer runs only while the view moves, with no
/// Rendering pump. Scale is interpolated geometrically, so a zoom feels even. With animations off the view jumps.
/// </summary>
internal sealed class ViewportAnimation(Core.Viewport.Viewport viewport)
{
    private DispatcherTimer? _timer;
    private (double Scale, Vec2 Offset) _from, _to;
    private DateTime _start;
    private TimeSpan _duration;

    public void Stop() => _timer?.Stop();

    public void To(double scale, Vec2 offset, TimeSpan duration)
    {
        if (!MotionSettings.AnimationsEnabled || duration == TimeSpan.Zero)
        {
            viewport.Set(scale, offset);
            return;
        }
        _from = (viewport.Scale, viewport.Offset);
        _to = (scale, offset);
        _start = DateTime.UtcNow;
        _duration = duration;
        _timer ??= CreateTimer();
        if (!_timer.IsEnabled) _timer.Start();
    }

    /// <summary>Zoom to <paramref name="scale"/> (clamped) keeping the world point under <paramref name="anchor"/> still.</summary>
    public void ZoomAt(Vec2 anchor, double scale, TimeSpan duration)
    {
        var s = Math.Clamp(scale, Core.Viewport.Viewport.MinScale, Core.Viewport.Viewport.MaxScale);
        var worldAnchor = viewport.ToWorld(anchor);
        To(s, new Vec2(anchor.X - worldAnchor.X * s, anchor.Y + worldAnchor.Y * s), duration);
    }

    private DispatcherTimer CreateTimer()
    {
        var t = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        t.Tick += (_, _) =>
        {
            var k = Math.Clamp((DateTime.UtcNow - _start).TotalMilliseconds / _duration.TotalMilliseconds, 0, 1);
            var e = MotionSettings.EaseInOut(k);
            viewport.Set(_from.Scale * Math.Pow(_to.Scale / _from.Scale, e), Vec2.Lerp(_from.Offset, _to.Offset, e));
            if (k >= 1) t.Stop();
        };
        return t;
    }
}
