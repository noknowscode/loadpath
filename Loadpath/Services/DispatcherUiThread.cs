using Loadpath.Presentation;
using Microsoft.UI.Dispatching;

namespace Loadpath.Services;

/// <summary>The window's dispatcher behind <see cref="IUiThread"/>: posts to it, and debounces with a DispatcherTimer.</summary>
public sealed class DispatcherUiThread(DispatcherQueue queue) : IUiThread
{
    public void Run(Action action)
    {
        if (queue.HasThreadAccess) action();
        else queue.TryEnqueue(() => action());
    }

    public IUiTimer CreateTimer(TimeSpan interval, Action tick) => new Timer(interval, tick);

    /// <summary>A DispatcherTimer ticks on the thread that created it, so it is created on the first restart, on the UI thread.</summary>
    private sealed class Timer(TimeSpan interval, Action tick) : IUiTimer
    {
        private DispatcherTimer? _timer;

        public void Restart()
        {
            if (_timer is null)
            {
                _timer = new DispatcherTimer { Interval = interval };
                _timer.Tick += (_, _) =>
                {
                    _timer.Stop();
                    tick();
                };
            }
            _timer.Stop();
            _timer.Start();
        }

        public void Stop() => _timer?.Stop();
    }
}
