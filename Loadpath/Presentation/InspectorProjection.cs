using System.Collections.Immutable;
using Loadpath.Core.Analysis;

namespace Loadpath.Presentation;

/// <summary>
/// The inspector as MVUX states: the summary, node, member or mixed content for the selection, the members connected to
/// a selected node, and the fields that edit a node's position and load and a member's section. A value typed into a
/// field is committed to the engine on the UI thread.
/// </summary>
public sealed class InspectorProjection(EditorEngine engine, IUiThread ui)
{
    private readonly Projection _projection = new();

    public IState<InspectorContent> Inspector => State.Value(this, () => InspectorContent.Empty);
    // The fields bind to X and Y (or Index) of these records; each record also names its element, so the callbacks,
    // which run after the edit, commit to the element the value was typed for and not to whatever is selected by then.
    public IState<NodeVector> NodePosition => State.Value(this, () => NodeVector.None).ForEach(async (v, ct) => CommitPosition(v));
    public IState<NodeVector> NodeLoad => State.Value(this, () => NodeVector.None).ForEach(async (v, ct) => CommitLoad(v));
    public IState<SectionPick> MemberSection => State.Value(this, () => SectionPick.None).ForEach(async (v, ct) => CommitSection(v));
    public IListState<ConnectedItem> ConnectedMembers => ListState<ConnectedItem>.Empty(this);
    public IListFeed<SectionChoice> Sections => ListFeed.Async(async ct => (IImmutableList<SectionChoice>)Section.Library.Select(s => new SectionChoice(s.Id, s.Name, s.ShortName)).ToImmutableList());

    private int? SelectedNodeId => engine.Selection.Single is { IsNode: true } r ? r.Id : null;
    private int? SelectedMemberId => engine.Selection.Single is { IsMember: true } r ? r.Id : null;

    // The element comes with the value (see NodeVector). A value the inspector itself pushed matches the document, so
    // committing it changes nothing.
    private void CommitPosition(NodeVector? v)
    {
        if (v is not null) ui.Run(() => engine.SetNodePosition(v.NodeId, new Vec2(v.X, v.Y)));
    }

    private void CommitLoad(NodeVector? v)
    {
        if (v is not null) ui.Run(() => engine.SetNodeLoad(v.NodeId, new Vec2(v.X, v.Y)));
    }

    private void CommitSection(SectionPick? v)
    {
        if (v is not null && v.Index >= 0 && v.Index < Section.Library.Count) ui.Run(() => engine.SetMemberSection(v.MemberId, Section.Library[v.Index]));
    }

    public async ValueTask RefreshAsync()
    {
        var t = _projection.Start();
        var doc = engine.Document;
        var sel = engine.Selection;
        InspectorContent c;
        if (sel.IsEmpty) c = Summary();
        else if (SelectedNodeId is { } nid && doc.FindNode(nid) is { } node)
        {
            c = ForNode(node);
            var connected = Connected(node);
            await ConnectedMembers.UpdateAsync(current => t.IsLatest ? (connected) : current);
            await NodePosition.UpdateAsync(current => t.IsLatest ? (new NodeVector(node.Id, node.Position.X, node.Position.Y)) : current);
            await NodeLoad.UpdateAsync(current => t.IsLatest ? (new NodeVector(node.Id, node.Load.X, node.Load.Y)) : current);
        }
        else if (SelectedMemberId is { } mid && doc.FindMember(mid) is { } member)
        {
            c = ForMember(member);
            await MemberSection.UpdateAsync(current => t.IsLatest ? (new SectionPick(member.Id, Math.Max(0, Section.Library.ToList().IndexOf(member.Section)))) : current);
        }
        else c = Mixed();
        await Inspector.UpdateAsync(current => t.IsLatest ? (c) : current);
    }

    private InspectorContent Summary()
    {
        var doc = engine.Document;
        var a = engine.Analysis;
        var pins = doc.Nodes.Count(n => n.Support == SupportKind.Pin);
        var rollers = doc.Nodes.Count(n => n.Support is SupportKind.RollerY or SupportKind.RollerX);
        var total = doc.Nodes.Aggregate(Vec2.Zero, (acc, n) => acc + n.Load);
        var mass = doc.Members.Sum(m => m.Section.MassPerMeter * doc.MemberLength(m));
        var reactions = a.IsSolved ? string.Join("\n", doc.Nodes.Where(n => n.HasSupport).Select(n => { var r = a.ForNode(n.Id)?.Reaction ?? Vec2.Zero; return $"N{n.Id}  {ResultText.Column(r.X)}  {ResultText.Column(r.Y)} kN"; })) : "";
        return InspectorContent.Empty with
        {
            IsSummary = true,
            Title = doc.Name,
            Subtitle = doc.Nodes.Count == 0 ? "Nothing here yet" : "Nothing selected",
            NodeCountText = doc.Nodes.Count.ToString(),
            MemberCountText = doc.Members.Count.ToString(),
            SupportsText = pins + rollers == 0 ? "none" : $"{pins} pin · {rollers} roller",
            TotalLoadText = total.LengthSquared < 1e-12 ? "none" : $"{total.Length:0.#} kN",
            MassText = doc.Members.Count == 0 ? "—" : $"{mass:0} kg",
            MaxUtil = a.IsSolved ? Math.Clamp(a.MaxUtilization, 0, 1) : 0,
            MaxUtilText = a.IsSolved ? $"{a.MaxUtilization * 100:0}%" : "—",
            MaxUtilToneKey = ResultText.UtilTone(a.MaxUtilization),
            MaxDeflectionText = a.IsSolved ? $"{a.MaxDisplacementM * 1000:0.0} mm" : "—",
            HasCritical = a.IsSolved && a.CriticalMemberId is not null,
            CriticalText = a.Failures.Count > 0 ? $"Step through failures · J" : a.CriticalMemberId is { } cid ? $"M{cid} governs" : "",
            ReactionsText = reactions,
            SummaryHint = a.Status switch
            {
                AnalysisStatus.Empty => "Press N and click to place nodes. M connects them.",
                AnalysisStatus.NoLoads => "Add a load with L to see forces.",
                AnalysisStatus.Unsupported => "Add a pin and a roller with S.",
                AnalysisStatus.Mechanism => "Triangulate the structure or add a support.",
                _ => a.MaxUtilization >= 1 ? "A member is over its limit. Select it to change its section." : "Drag any node to watch the load path change.",
            },
        };
    }

    private InspectorContent ForNode(Node node)
    {
        var a = engine.Analysis;
        var r = a.ForNode(node.Id);
        var disp = a.IsSolved && r is { } dn ? dn.Displacement * 1000 : Vec2.Zero;
        var hasDisp = a.IsSolved && r is not null;
        // Mini diagram: 72 px box, direction of travel drawn at a fixed 24 px so small motions still read.
        var dotX = 36.0; var dotY = 36.0;
        if (hasDisp && disp.Length > 1e-6) { dotX = 36 + disp.X / disp.Length * 24; dotY = 36 - disp.Y / disp.Length * 24; }
        var kindText = node.Support switch
        {
            SupportKind.Pin => "Pinned support",
            SupportKind.RollerY => "Roller support, free in x",
            SupportKind.RollerX => "Roller support, free in y",
            _ => "Free node",
        };
        return InspectorContent.Empty with
        {
            NodeDescription = kindText + (node.HasLoad ? $" with a {node.Load.Length:0.#} kN point load" : ""),
            DispXText = hasDisp ? $"{disp.X:+0.00;−0.00} mm" : "—",
            DispYText = hasDisp ? $"{disp.Y:+0.00;−0.00} mm" : "—",
            DispMagText = hasDisp ? $"{disp.Length:0.00} mm" : "—",
            DispDotX = dotX, DispDotY = dotY,
            ConnectedCountText = engine.Document.MembersAt(node.Id).Count().ToString(),
            IsSummary = false, IsNode = true, SelectionHasNodes = true, SelectionLabel = $"N{node.Id}",
            Title = $"Node N{node.Id}",
            Subtitle = node.Support.Label() + (node.HasLoad ? $" · {node.Load.Length:0.#} kN" : ""),
            IsPin = node.Support == SupportKind.Pin, IsRoller = node.Support == SupportKind.RollerY,
            IsRollerX = node.Support == SupportKind.RollerX, IsFree = node.Support == SupportKind.None,
            HasReaction = a.IsSolved && node.HasSupport && r is not null,
            ReactionText = a.IsSolved && node.HasSupport && r is { } rr ? $"{ResultText.Column(rr.Reaction.X)}  {ResultText.Column(rr.Reaction.Y)} kN" : "",
        };
    }

    private IImmutableList<ConnectedItem> Connected(Node node)
    {
        var a = engine.Analysis;
        return engine.Document.MembersAt(node.Id).Select(m =>
        {
            var mr = a.IsSolved ? a.For(m.Id) : null;
            return new ConnectedItem($"m{m.Id}", $"M{m.Id}", $"N{m.StartNodeId}–N{m.EndNodeId}",
                mr is { } f ? (Math.Abs(f.AxialForceKn) < 0.05 ? "0.0" : ResultText.SignedKn(f.AxialForceKn)) : "—",
                mr is { } t && t.IsOverstressed ? "DangerBrush" : "InkBrush",
                mr is { } u ? $"{u.Utilization * 100:0}%" : "",
                mr is { IsOverstressed: true });
        }).ToImmutableList();
    }

    private InspectorContent ForMember(Member member)
    {
        var doc = engine.Document;
        var a = engine.Analysis;
        var res = a.For(member.Id);
        var solved = a.IsSolved && res is not null;
        var mr = res ?? default;
        return InspectorContent.Empty with
        {
            IsSummary = false, IsMember = true, SelectionLabel = $"M{member.Id}",
            Title = $"Member M{member.Id}",
            Subtitle = member.Section.Name,
            EndpointsText = $"N{member.StartNodeId} → N{member.EndNodeId}",
            LengthText = $"{doc.MemberLength(member):0.000} m",
            MaterialText = $"{member.Section.Material.Name} · E {member.Section.Material.ElasticModulusGPa:0} GPa · fy {member.Section.Material.YieldStrengthMPa:0} MPa",
            AreaText = $"A {member.Section.AreaMm2:0} mm²  ·  I {member.Section.SecondMomentMm4 / 1e4:0.#} cm⁴",
            ForceKindText = solved ? (mr.IsTension ? "Tension" : mr.IsCompression ? "Compression" : "Zero force") : "Not solved",
            ForceToneKey = solved ? ResultText.ForceTone(mr.IsTension, mr.IsCompression, mr.IsOverstressed) : "InkTertiaryBrush",
            ForceText = solved ? $"{Math.Abs(mr.AxialForceKn):0.00} kN" : "—",
            StressText = solved ? $"{Math.Abs(mr.StressMPa):0.0} MPa" : "—",
            Util = solved ? Math.Clamp(mr.Utilization, 0, 1) : 0,
            UtilText = solved ? $"{mr.Utilization * 100:0}%" : "—",
            UtilToneKey = solved ? ResultText.UtilTone(mr.Utilization) : "OkBrush",
            GovernsText = solved ? mr.Failure switch
            {
                FailureMode.Buckling => "Fails by buckling: the strut bows out",
                FailureMode.Yield => mr.IsTension ? "Fails by yield: the tie stretches" : "Fails by yield: the strut crushes",
                _ => mr.BucklingGoverns ? "Buckling governs" : "Yield governs",
            } : "",
            BucklingText = solved ? (mr.IsCompression ? $"Pcr {member.Section.CriticalBucklingLoadN(mr.LengthM) / 1000:0.0} kN · {mr.UtilizationBuckling * 100:0}%" : "n/a (tension)") : "—",
        };
    }

    private InspectorContent Mixed()
    {
        var sel = engine.Selection;
        return InspectorContent.Empty with
        {
            IsSummary = false, IsMixed = true, SelectionHasNodes = sel.NodeCount > 0, SelectionLabel = $"{sel.Count} selected",
            Title = $"{sel.Count} selected",
            Subtitle = $"{ResultText.Plural(sel.NodeCount, "node")} · {ResultText.Plural(sel.MemberCount, "member")}",
            MixedHasNodes = sel.NodeCount > 0,
            MixedHasMembers = sel.MemberCount > 0,
            MixedText = "Bulk actions apply to every selected element.",
        };
    }
}
