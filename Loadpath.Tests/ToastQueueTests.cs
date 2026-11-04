using Loadpath.Presentation;
using Xunit;

namespace Loadpath.Tests;

/// <summary>One toast at a time: notes give way to newer notes, failures are never replaced before they are read.</summary>
public class ToastQueueTests
{
    private static readonly ToastMessage NoteA = new("Pasted Warren truss", false);
    private static readonly ToastMessage NoteB = new("Nothing selected", false);
    private static readonly ToastMessage SettingsFailed = new("Settings not saved: Access to the path 'settings.json' is denied", true);
    private static readonly ToastMessage AutosaveFailed = new("Autosave failed: Access to the path 'autosave.loadpath' is denied. Save to keep your work.", true);

    private static ToastQueue Queue() => new(new EditorEngine());

    [Fact]
    public void A_newer_note_replaces_the_note_on_screen()
    {
        var q = Queue();
        q.Show(NoteA);
        q.Show(NoteB);

        Assert.Equal(NoteB, q.Current);
        Assert.Empty(q.Waiting);
    }

    [Fact]
    public void Two_failures_at_launch_are_both_shown_in_turn()
    {
        var q = Queue();
        q.Show(SettingsFailed);
        q.Show(AutosaveFailed);

        Assert.Equal(SettingsFailed, q.Current);
        q.Expire();
        Assert.Equal(AutosaveFailed, q.Current);
        q.Expire();
        Assert.Null(q.Current);
    }

    [Fact]
    public void A_failure_replaces_a_note_but_a_note_waits_behind_a_failure()
    {
        var q = Queue();
        q.Show(NoteA);
        q.Show(SettingsFailed);
        q.Show(NoteB);

        Assert.Equal(SettingsFailed, q.Current);
        Assert.Equal(new[] { NoteB }, q.Waiting);
    }

    [Fact]
    public void Behind_a_failure_only_the_newest_note_waits_and_repeats_add_nothing()
    {
        var q = Queue();
        q.Show(SettingsFailed);
        q.Show(NoteA);
        q.Show(AutosaveFailed);
        q.Show(NoteB);
        q.Show(SettingsFailed);
        q.Show(AutosaveFailed);

        Assert.Equal(new[] { AutosaveFailed, NoteB }, q.Waiting);
    }

    [Fact]
    public void Every_change_of_the_toast_on_screen_is_announced()
    {
        var q = Queue();
        var changes = 0;
        q.CurrentChanged += (_, _) => changes++;

        q.Show(NoteA);
        q.Show(NoteA);
        q.Expire();

        Assert.Equal(3, changes);
    }

    [Fact]
    public void The_engine_feeds_the_queue_and_marks_its_failures()
    {
        var engine = new EditorEngine();
        var q = new ToastQueue(engine);

        engine.ToastError("Could not save: Access to the path '/home/ann/Documents/Loadpath/Bridge.loadpath' is denied.");
        engine.Toast("Saved Bridge.loadpath");

        Assert.Equal(new ToastMessage("Could not save: Access to the path 'Bridge.loadpath' is denied.", true), q.Current);
        Assert.Equal("Saved Bridge.loadpath", Assert.Single(q.Waiting).Text);
    }

    [Fact]
    public void A_failure_stays_longer_than_a_note()
    {
        Assert.True(ToastQueue.DurationOf(SettingsFailed) > ToastQueue.DurationOf(NoteA));
    }

    [Theory]
    [InlineData("Access to the path '/home/ann/.local/share/Loadpath/settings.json' is denied.", "Access to the path 'settings.json' is denied.")]
    [InlineData("Could not find a part of the path 'C:\\Users\\ann\\Documents\\Loadpath\\Roof.loadpath'.", "Could not find a part of the path 'Roof.loadpath'.")]
    [InlineData("The process cannot access the file \"/tmp/x/autosave.loadpath\" because it is in use", "The process cannot access the file \"autosave.loadpath\" because it is in use")]
    [InlineData("Node 1 appears twice.", "Node 1 appears twice.")]
    [InlineData("Quoted 'words' stay as they are", "Quoted 'words' stay as they are")]
    public void Paths_in_messages_read_as_file_names(string message, string shown)
    {
        Assert.Equal(shown, ToastText.ShortenPaths(message));
    }

    [Theory]
    [InlineData("/home/ann/Documents/Loadpath/Untitled.share.txt", "Loadpath/Untitled.share.txt")]
    [InlineData("C:\\Users\\ann\\Documents\\Loadpath\\Untitled.share.txt", "Loadpath/Untitled.share.txt")]
    [InlineData("Untitled.share.txt", "Untitled.share.txt")]
    public void A_share_file_is_named_with_its_folder(string path, string shown)
    {
        Assert.Equal(shown, ToastText.FileInFolder(path));
    }
}
