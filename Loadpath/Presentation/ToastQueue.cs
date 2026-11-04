using System.Text.RegularExpressions;

namespace Loadpath.Presentation;

/// <summary>One toast: what it says, and whether it reports a failure.</summary>
public sealed record ToastMessage(string Text, bool IsError);

/// <summary>
/// The toast line under the canvas, one message at a time. A newer note replaces the one on screen (the result of the
/// last action is the one that matters), but a failure is never replaced before it has had its time: notes and other
/// failures wait behind it, so two failures at launch (settings and autosave) are both read. The view shows
/// <see cref="Current"/> and calls <see cref="Expire"/> when its time is up.
/// </summary>
public sealed class ToastQueue
{
    private readonly List<ToastMessage> _waiting = new();

    /// <summary>How long a note stays, and how long a failure stays (it is longer and must be read).</summary>
    public static readonly TimeSpan NoteDuration = TimeSpan.FromMilliseconds(2400);
    public static readonly TimeSpan ErrorDuration = TimeSpan.FromMilliseconds(4500);

    public ToastQueue(EditorEngine engine) => engine.ToastRequested += (_, toast) => Show(toast);

    /// <summary>The toast on screen, or null when there is none.</summary>
    public ToastMessage? Current { get; private set; }

    /// <summary>Messages waiting behind a failure, oldest first.</summary>
    public IReadOnlyList<ToastMessage> Waiting => _waiting;

    /// <summary>Raised when <see cref="Current"/> changes, including to the same text again (the view restarts its timer).</summary>
    public event EventHandler? CurrentChanged;

    public void Show(ToastMessage toast)
    {
        if (Current is null || !Current.IsError)
        {
            Current = toast;
            CurrentChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        // A failure is on screen: the new message waits. A repeat of anything already shown or waiting adds nothing,
        // and a newer note replaces a waiting note.
        if (toast == Current || _waiting.Contains(toast)) return;
        if (!toast.IsError) _waiting.RemoveAll(w => !w.IsError);
        _waiting.Add(toast);
    }

    /// <summary>The toast on screen has had its time: show the next one waiting, or none.</summary>
    public void Expire()
    {
        if (_waiting.Count > 0)
        {
            Current = _waiting[0];
            _waiting.RemoveAt(0);
        }
        else
        {
            Current = null;
        }
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public static TimeSpan DurationOf(ToastMessage toast) => toast.IsError ? ErrorDuration : NoteDuration;
}

/// <summary>Toast wording helpers.</summary>
public static partial class ToastText
{
    /// <summary>A quoted absolute path in a system message: '/home/ann/.local/share/Loadpath/settings.json', "C:\Users\ann\x.loadpath".</summary>
    [GeneratedRegex("""(['"])((?:[A-Za-z]:[\\/]|[\\/])[^'"]*)\1""")]
    private static partial Regex QuotedPath();

    /// <summary>
    /// A toast names a file, not where it lives: the folders above it make the line wrap on a phone and say nothing
    /// the user can act on. "Access to the path '/home/ann/.local/share/Loadpath/settings.json' is denied" reads
    /// "Access to the path 'settings.json' is denied". The full path goes to the log.
    /// </summary>
    public static string ShortenPaths(string message) =>
        QuotedPath().Replace(message, m => m.Groups[1].Value + FileName(m.Groups[2].Value) + m.Groups[1].Value);

    /// <summary>The last part of a path with either separator (a Windows path read on any host).</summary>
    public static string FileName(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        var name = trimmed[(trimmed.LastIndexOfAny(['/', '\\']) + 1)..];
        return name.Length > 0 ? name : path;
    }

    /// <summary>A file and the folder it is in ("Loadpath/Untitled.share.txt"): enough to find it, short enough for a phone.</summary>
    public static string FileInFolder(string path)
    {
        var trimmed = path.TrimEnd('/', '\\');
        var cut = trimmed.LastIndexOfAny(['/', '\\']);
        if (cut <= 0) return FileName(path);
        return $"{FileName(trimmed[..cut])}/{trimmed[(cut + 1)..]}";
    }
}
