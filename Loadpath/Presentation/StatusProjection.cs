using System.Collections.Immutable;
using Loadpath.Core.Analysis;

namespace Loadpath.Presentation;

/// <summary>
/// What the analysis says, as MVUX states: the status (title pill, banners, undo and redo, the status bar's readouts),
/// the counts beside each reduce-noise layer, and the summary inspector's list of members over capacity.
/// </summary>
public sealed class StatusProjection(EditorEngine engine)
{
    private readonly Projection _status = new(), _failures = new();

    public IState<EditorStatus> Status => State.Value(this, () => EditorStatus.Initial);
    public IState<LayerCounts> Counts => State.Value(this, () => LayerCounts.Empty);

    public IListState<FailureItem> Failures => ListState<FailureItem>.Empty(this);
    public IState<bool> HasFailures => State.Value(this, () => false);
    /// <summary>Solved with nothing failing: the moment "Lighten members" applies.</summary>
    public IState<bool> CanLighten => State.Value(this, () => false);
    public IState<string> FailuresHeader => State.Value(this, () => "");

    public async ValueTask RefreshStatusAsync()
    {
        var t = _status.Start();
        var a = engine.Analysis;
        var h = engine.History;
        string text, detail, tone, mechanismText = "";
        var mechanism = false;
        switch (a.Status)
        {
            case AnalysisStatus.Empty: text = "Empty"; detail = "Place a node to begin"; tone = "OnSurfaceVariantBrush"; break;
            case AnalysisStatus.NoLoads: text = "No loads"; detail = "Add a load (L) to see forces"; tone = "OnSurfaceVariantBrush"; break;
            case AnalysisStatus.Unsupported:
                text = "Unsupported"; detail = "A pin and a roller are needed"; tone = "ErrorBrush"; mechanism = true;
                mechanismText = "Unsupported — the structure can move freely. Add a pin and a roller (S)."; break;
            case AnalysisStatus.Mechanism:
                text = "Mechanism"; detail = "The structure can move freely"; tone = "ErrorBrush"; mechanism = true;
                mechanismText = "Mechanism — the structure can move freely. Triangulate it or add a support."; break;
            default:
                text = a.MaxUtilization >= 1 ? "Overstressed" : "Solved";
                detail = $"{a.SolveMilliseconds:0.0} ms";
                tone = a.MaxUtilization >= 1 ? "ErrorBrush" : "OnSurfaceVariantBrush";
                break;
        }
        var d = a.DetachedNodeIds.Count;
        var over = a.IsSolved ? a.Members.Values.Count(m => m.IsOverstressed) : 0;
        var pillText = over > 0 ? $"{over} OVER CAPACITY"
            : a.IsSolved ? "WITHIN CAPACITY" : $"{text} · {detail}".ToUpperInvariant();
        var status = new EditorStatus(
            engine.Document.Name, engine.IsDirty, tone, mechanism, mechanismText,
            a.HasDetached && !mechanism,
            d == 1 ? "1 node is not connected to a support and carries nothing." : $"{d} nodes are not connected to a support and carry nothing.",
            a.IsSolved ? $"{a.MaxUtilization * 100:0}%" : "—", ResultText.UtilTone(a.MaxUtilization),
            engine.Document.Nodes.Count == 0, h.CanUndo, h.CanRedo,
            h.UndoLabel is { } u ? $"Undo {u.ToLowerInvariant()}" : "Nothing to undo",
            h.RedoLabel is { } r ? $"Redo {r.ToLowerInvariant()}" : "Nothing to redo",
            pillText, over > 0 || mechanism,
            a.IsSolved ? $"{a.SolveMilliseconds:0.0} ms" : "—", over > 0);
        await Status.UpdateAsync(current => t.IsLatest ? (status) : current);

        var doc = engine.Document;
        var supports = doc.Nodes.Count(n => n.HasSupport);
        await Counts.UpdateAsync(current => !t.IsLatest ? current : new LayerCounts(
            $"{engine.Options.GridStep:0.##} m",
            supports.ToString(),
            doc.Nodes.Count(n => n.HasLoad).ToString(),
            over.ToString(),
            (a.IsSolved ? doc.Members.Count - over : 0).ToString(),
            (a.IsSolved ? supports : 0).ToString()));
    }

    public async ValueTask RefreshFailuresAsync()
    {
        var t = _failures.Start();
        var doc = engine.Document;
        var failures = engine.Analysis.Failures;
        var items = failures.Select(f =>
        {
            var verb = EditorEngine.FailureVerb(f.Failure);
            var m = doc.FindMember(f.MemberId);
            var detail = m is null ? "" : f.Failure == FailureMode.Buckling
                ? $"{Math.Abs(f.AxialForceKn):0.0} kN on Pcr {m.Section.CriticalBucklingLoadN(f.LengthM) / 1000:0.0} kN"
                : $"{Math.Abs(f.StressMPa):0} MPa on fy {m.Section.Material.YieldStrengthMPa:0} MPa";
            return new FailureItem($"m{f.MemberId}", $"M{f.MemberId}", verb.ToUpperInvariant(), $"{f.Utilization * 100:0}%", detail,
                $"Select member M{f.MemberId}, {verb} at {f.Utilization * 100:0} percent");
        }).ToImmutableList();
        await Failures.UpdateAsync(current => t.IsLatest ? (items) : current);
        await HasFailures.UpdateAsync(current => t.IsLatest ? (items.Count > 0) : current);
        var canLighten = engine.Analysis.IsSolved && items.Count == 0 && engine.Document.Members.Count > 0;
        await CanLighten.UpdateAsync(current => t.IsLatest ? canLighten : current);
        await FailuresHeader.UpdateAsync(current => t.IsLatest ? (items.Count == 1 ? "1 MEMBER" : $"{items.Count} MEMBERS") : current);
    }
}
