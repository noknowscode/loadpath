namespace Loadpath.Presentation;

/// <summary>
/// Each engine event starts a projection, so projections of one kind can run at the same time, and MVUX does not apply
/// concurrent state updates in the order they were made: an older projection could land after a newer one and leave the
/// view stale (a status still reading "Undo open" after an edit). A projection takes a ticket when it starts; its updates
/// apply only while it is still the latest of its kind, and keep what is there otherwise.
/// </summary>
internal sealed class Projection
{
    private long _latest;

    public Ticket Start() => new(this, Interlocked.Increment(ref _latest));

    public readonly record struct Ticket(Projection Kind, long Number)
    {
        public bool IsLatest => Interlocked.Read(ref Kind._latest) == Number;
    }
}
