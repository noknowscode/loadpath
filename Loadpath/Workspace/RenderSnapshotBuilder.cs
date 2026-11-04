using Loadpath.Presentation;
using SkiaSharp;

namespace Loadpath.Workspace;

/// <summary>
/// The frame the renderer draws, built from the engine: every node and member in screen space (and deflected), what is
/// selected, hovered and dimmed, the part names, and which layers show. Built on the UI thread, which owns the document.
/// </summary>
public static class RenderSnapshotBuilder
{
    public static RenderSnapshot Build(EditorEngine ed, float width, float height, bool railOverlay)
    {
        var doc = ed.Document;
        var vp = ed.Viewport;
        var analysis = ed.Analysis;
        var sel = ed.Selection;
        var hover = ed.Interaction.Hover;
        var options = ed.Options;
        var anySelected = !sel.IsEmpty;
        var dimOthers = sel.MemberCount == 1 && sel.NodeCount == 0;
        var (minor, major) = GridSteps.For(vp.Scale);

        // Deflection exaggeration: max displacement reads as 6% of the structure diagonal, times the user factor.
        var bounds = doc.GetBounds();
        var diag = bounds.IsEmpty ? 1 : Math.Max(1, Math.Sqrt(bounds.Width * bounds.Width + bounds.Height * bounds.Height));
        var exag = analysis.IsSolved && analysis.MaxDisplacementM > 1e-12 ? diag * 0.06 * options.Exaggeration / analysis.MaxDisplacementM : 0;

        var connected = new HashSet<int>();
        if (dimOthers && doc.FindMember(sel.MemberIds.First()) is { } sm) { connected.Add(sm.StartNodeId); connected.Add(sm.EndNodeId); }
        var detached = analysis.DetachedNodeIds.Count > 0 ? new HashSet<int>(analysis.DetachedNodeIds) : null;

        var nodes = new RenderSnapshot.NodeItem[doc.Nodes.Count];
        var nodeIndex = new Dictionary<int, int>(doc.Nodes.Count);
        for (var i = 0; i < doc.Nodes.Count; i++)
        {
            var n = doc.Nodes[i];
            nodeIndex[n.Id] = i;
            var s = vp.ToScreen(n.Position);
            var r = analysis.ForNode(n.Id);
            var disp = r?.Displacement ?? Vec2.Zero;
            var deflected = vp.ToScreen(n.Position + disp * exag);
            var tail = HitTester.LoadHandleScreen(n, vp);
            var isSelected = sel.Contains(n.Ref);
            var dim = (dimOthers && !connected.Contains(n.Id)) || (detached?.Contains(n.Id) ?? false);
            nodes[i] = new RenderSnapshot.NodeItem(n.Id, P(s), P(deflected), n.Support, n.Load, P(tail), r?.Reaction ?? Vec2.Zero, isSelected, hover == n.Ref, dim, n.Position);
        }

        // Part names for the annotation layer: classified from world geometry, so they do not change with zoom.
        var geometry = new (Vec2 A, Vec2 B)[doc.Members.Count];
        for (var i = 0; i < doc.Members.Count; i++) geometry[i] = (doc.Nodes[nodeIndex[doc.Members[i].StartNodeId]].Position, doc.Nodes[nodeIndex[doc.Members[i].EndNodeId]].Position);
        var parts = PartClassifier.Classify(geometry);
        var supports = doc.Nodes.Where(n => n.HasSupport).Select(n => n.Position).ToList();

        var members = new RenderSnapshot.MemberItem[doc.Members.Count];
        for (var i = 0; i < doc.Members.Count; i++)
        {
            var m = doc.Members[i];
            var a = nodes[nodeIndex[m.StartNodeId]];
            var b = nodes[nodeIndex[m.EndNodeId]];
            var r = analysis.For(m.Id);
            var isSelected = sel.Contains(m.Ref);
            var dim = (dimOthers && !isSelected) || (detached?.Contains(m.StartNodeId) ?? false);
            members[i] = new RenderSnapshot.MemberItem(m.Id, a.Screen, b.Screen, a.Deflected, b.Deflected,
                r?.AxialForceKn ?? 0, r?.Utilization ?? 0, r?.IsOverstressed ?? false, r?.BucklingGoverns ?? false, isSelected, hover == m.Ref, dim, parts[i]);
        }

        return new RenderSnapshot
        {
            Nodes = nodes,
            Members = members,
            MaxAbsForceKn = analysis.MaxAbsForceKn,
            Status = analysis.Status,
            Scale = vp.Scale,
            Origin = P(vp.ToScreen(Vec2.Zero)),
            Mode = options.DisplayMode,
            ShowDeflection = options.IsVisible(NoiseLayer.Deflection),
            ShowLabels = options.IsVisible(NoiseLayer.MemberForces),
            ShowReactions = options.IsVisible(NoiseLayer.Reactions),
            ShowGrid = options.IsVisible(NoiseLayer.Grid),
            ShowDimensions = options.IsVisible(NoiseLayer.Dimensions),
            ShowSupports = options.IsVisible(NoiseLayer.Supports),
            ShowLoads = options.IsVisible(NoiseLayer.Loads),
            ShowOverCapacity = options.IsVisible(NoiseLayer.OverCapacity),
            ShowPartNames = options.IsVisible(NoiseLayer.PartNames),
            ShowLegend = options.IsVisible(NoiseLayer.Legend),
            StructureKind = PartClassifier.Describe(geometry, parts, supports),
            GridMinor = minor,
            GridMajor = major,
            Overlay = ed.Interaction.Overlay,
            Width = width,
            Height = height,
            AnySelected = anySelected,
            DocumentName = doc.Name,
            Exaggeration = options.Exaggeration,
            Center = bounds.IsEmpty ? new SKPoint(width / 2, height / 2) : P(vp.ToScreen(bounds.Center)),
            RailOverlay = railOverlay,
        };
    }

    private static SKPoint P(Vec2 v) => new((float)v.X, (float)v.Y);
}
