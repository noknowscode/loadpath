using Loadpath.Controls;
using Loadpath.Presentation;

namespace Loadpath.Views;

/// <summary>Shows the <see cref="ToastQueue"/>'s current message for its time, then asks the queue for the next.</summary>
public sealed partial class ToastHost : UserControl
{
    private readonly DispatcherTimer _timer = new();
    private ToastQueue? _queue;

    public ToastHost()
    {
        InitializeComponent();
        _timer.Tick += (_, _) =>
        {
            _timer.Stop();
            _queue?.Expire();
        };
    }

    /// <summary>The queue this host shows. Set once by the page.</summary>
    public ToastQueue? Queue
    {
        get => _queue;
        set
        {
            if (_queue is not null) _queue.CurrentChanged -= OnCurrentChanged;
            _queue = value;
            if (_queue is not null) _queue.CurrentChanged += OnCurrentChanged;
        }
    }

    // A toast can be raised from any thread (a background failure); the view changes on its own.
    // xaml-lint: allow codebehind - the entrance motion and the lifetime timer start with each message the queue hands over
    private void OnCurrentChanged(object? sender, EventArgs e)
    {
        if (DispatcherQueue.HasThreadAccess) ShowCurrent();
        else DispatcherQueue.TryEnqueue(ShowCurrent);
    }

    private void ShowCurrent()
    {
        _timer.Stop();
        if (_queue?.Current is not { } toast)
        {
            Motion.Exit(Chrome);
            return;
        }
        Message.Text = toast.Text;
        // xaml-lint: allow codebehind - the mark follows the message on screen, which the queue hands over imperatively
        ErrorMark.Visibility = toast.IsError ? Visibility.Visible : Visibility.Collapsed;
        _timer.Interval = ToastQueue.DurationOf(toast);
        _timer.Start();
        Motion.Enter(Chrome, decorative: true);
    }
}
