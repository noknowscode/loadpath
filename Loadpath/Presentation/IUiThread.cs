namespace Loadpath.Presentation;

/// <summary>
/// The UI thread, as the presentation layer needs it. MVUX runs commands and state callbacks off the UI thread, while
/// the engine raises events that reach XAML, so every engine mutation is posted back here. The app hands in the window's
/// dispatcher (Services/DispatcherUiThread); the tests hand in one that runs at once and fires timers on demand.
/// </summary>
public interface IUiThread
{
    /// <summary>True when the caller is on the UI thread.</summary>
    bool HasAccess { get; }

    /// <summary>Run on the UI thread: now when the caller is on it, otherwise as soon as the thread is free.</summary>
    void Run(Action action);

    /// <summary>A one-shot timer that ticks on the UI thread <paramref name="interval"/> after its last restart (a debounce).</summary>
    IUiTimer CreateTimer(TimeSpan interval, Action tick);
}

/// <summary>A debounce timer from <see cref="IUiThread.CreateTimer"/>.</summary>
public interface IUiTimer
{
    /// <summary>Start, or push the tick back by a full interval when it is already running.</summary>
    void Restart();

    void Stop();
}
