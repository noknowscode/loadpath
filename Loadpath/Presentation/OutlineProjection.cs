using System.Collections.Immutable;

namespace Loadpath.Presentation;

/// <summary>
/// The structure outline as MVUX states: a row per node and per member with the selection marked, the section headers
/// and whether each section is expanded.
/// </summary>
public sealed class OutlineProjection(EditorEngine engine)
{
    private readonly Projection _projection = new();

    public IListState<OutlineItem> OutlineNodes => ListState<OutlineItem>.Empty(this);
    public IListState<OutlineItem> OutlineMembers => ListState<OutlineItem>.Empty(this);
    public IState<string> NodesHeader => State.Value(this, () => "Nodes");
    public IState<string> MembersHeader => State.Value(this, () => "Members");
    public IState<bool> OutlineIsEmpty => State.Value(this, () => true);
    public IState<bool> NodesExpanded => State.Value(this, () => true);
    public IState<bool> MembersExpanded => State.Value(this, () => true);

    public async ValueTask RefreshAsync()
    {
        var t = _projection.Start();
        var doc = engine.Document;
        var a = engine.Analysis;
        var sel = engine.Selection;
        var nodes = doc.Nodes.Select(n =>
        {
            var selected = sel.Contains(n.Ref);
            // Round, then add +0.0: -0.0 + 0.0 is +0.0, so a node at x = -1e-12 reads "0" instead of "-0".
            return new OutlineItem($"n{n.Id}", $"N{n.Id}", $"{Math.Round(n.Position.X, 2) + 0.0:0.##}, {Math.Round(n.Position.Y, 2) + 0.0:0.##}",
                n.HasLoad ? $"{ResultText.Arrow(n.Load)} {n.Load.Length:0.#} kN" : "",
                "InkBrush", selected ? "SelectionBrush" : "TransparentBrush", n.HasSupport, ResultText.SupportWord(n.Support), false, "");
        }).ToImmutableList();
        var members = doc.Members.Select(m =>
        {
            var selected = sel.Contains(m.Ref);
            var r = a.For(m.Id);
            var solved = a.IsSolved && r is { } rr;
            var res = r ?? default;
            return new OutlineItem($"m{m.Id}", $"M{m.Id}", $"N{m.StartNodeId}-N{m.EndNodeId}",
                solved ? (Math.Abs(res.AxialForceKn) < 0.05 ? "0.0" : ResultText.SignedKn(res.AxialForceKn)) : "",
                solved ? (res.IsOverstressed ? "DangerBrush" : "InkBrush") : "InkTertiaryBrush",
                selected ? "SelectionBrush" : "TransparentBrush", false, "",
                solved && res.IsOverstressed, solved ? $"{res.Utilization * 100:0}%" : "");
        }).ToImmutableList();
        await OutlineNodes.UpdateAsync(current => t.IsLatest ? (nodes) : current);
        await OutlineMembers.UpdateAsync(current => t.IsLatest ? (members) : current);
        await NodesHeader.UpdateAsync(current => t.IsLatest ? (doc.Nodes.Count.ToString()) : current);
        await MembersHeader.UpdateAsync(current => t.IsLatest ? (doc.Members.Count.ToString()) : current);
        await OutlineIsEmpty.UpdateAsync(current => t.IsLatest ? (doc.Nodes.Count == 0) : current);
    }
}
