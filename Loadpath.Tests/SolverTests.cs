using Loadpath.Core.Analysis;
using Loadpath.Core.Editing;
using Loadpath.Core.Geometry;
using Loadpath.Core.Model;
using Loadpath.Core.Samples;
using Xunit;

namespace Loadpath.Tests;

public class SolverTests
{
    private static StructureDocument Triangle()
    {
        // Equilateral-ish triangle: pin at A(0,0), roller at B(4,0), apex C(2,3) loaded 10 kN down.
        var doc = new StructureDocument();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(0, 0)));
        h.Do(new AddNodeEdit(new Vec2(4, 0)));
        h.Do(new AddNodeEdit(new Vec2(2, 3)));
        h.Do(new SetSupportEdit(1, SupportKind.Pin));
        h.Do(new SetSupportEdit(2, SupportKind.RollerY));
        h.Do(new SetLoadEdit(3, new Vec2(0, -10)));
        h.Do(new AddMemberEdit(1, 3, Section.Chs48));
        h.Do(new AddMemberEdit(2, 3, Section.Chs48));
        h.Do(new AddMemberEdit(1, 2, Section.Chs48));
        return doc;
    }

    [Fact]
    public void Triangle_matches_method_of_joints()
    {
        var r = TrussSolver.Solve(Triangle());
        Assert.Equal(AnalysisStatus.Solved, r.Status);
        // Legs: length 3.606 m, sin = 3/3.606. Each leg carries 5 kN vertical → F = -5 / 0.832 = -6.009 kN (compression).
        var leg = r.Members[1].AxialForceKn;
        Assert.InRange(leg, -6.02, -6.00);
        Assert.InRange(r.Members[2].AxialForceKn, -6.02, -6.00);
        // Bottom chord in tension: 6.009 * cos = 6.009 * 2/3.606 = 3.333 kN.
        Assert.InRange(r.Members[3].AxialForceKn, 3.32, 3.34);
        // Reactions: 5 kN up at each support.
        Assert.InRange(r.Nodes[1].Reaction.Y, 4.99, 5.01);
        Assert.InRange(r.Nodes[2].Reaction.Y, 4.99, 5.01);
        Assert.InRange(r.Nodes[1].Reaction.X, -0.01, 0.01);
        // Apex moves down.
        Assert.True(r.Nodes[3].Displacement.Y < 0);
    }

    [Fact]
    public void Missing_supports_is_reported()
    {
        var doc = Triangle();
        new EditHistory(doc).Do(new SetSupportEdit(2, SupportKind.None));
        var r = TrussSolver.Solve(doc);
        Assert.Equal(AnalysisStatus.Unsupported, r.Status);
    }

    [Fact]
    public void Mechanism_is_detected()
    {
        // A square without a diagonal is a mechanism.
        var doc = new StructureDocument();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(0, 0)));
        h.Do(new AddNodeEdit(new Vec2(2, 0)));
        h.Do(new AddNodeEdit(new Vec2(2, 2)));
        h.Do(new AddNodeEdit(new Vec2(0, 2)));
        h.Do(new SetSupportEdit(1, SupportKind.Pin));
        h.Do(new SetSupportEdit(2, SupportKind.RollerY));
        h.Do(new SetLoadEdit(3, new Vec2(5, 0)));
        h.Do(new AddMemberEdit(1, 2, Section.Chs48));
        h.Do(new AddMemberEdit(2, 3, Section.Chs48));
        h.Do(new AddMemberEdit(3, 4, Section.Chs48));
        h.Do(new AddMemberEdit(4, 1, Section.Chs48));
        Assert.Equal(AnalysisStatus.Mechanism, TrussSolver.Solve(doc).Status);
        h.Do(new AddMemberEdit(1, 3, Section.Chs48));
        Assert.Equal(AnalysisStatus.Solved, TrussSolver.Solve(doc).Status);
    }

    [Fact]
    public void Warren_sample_is_symmetric_and_in_equilibrium()
    {
        var doc = new StructureDocument();
        SampleStructures.Build("warren").Restore(doc);
        var r = TrussSolver.Solve(doc);
        Assert.Equal(AnalysisStatus.Solved, r.Status);
        var totalLoad = doc.Nodes.Sum(n => n.Load.Y);
        var totalReaction = r.Nodes.Values.Sum(n => n.Reaction.Y);
        Assert.InRange(totalReaction + totalLoad, -1e-6, 1e-6);
        // First and last bottom chord members mirror each other.
        Assert.InRange(r.Members[1].AxialForceKn - r.Members[6].AxialForceKn, -1e-6, 1e-6);
        Assert.True(r.MaxUtilization > 0 && r.MaxUtilization < 1);
        Assert.NotNull(r.CriticalMemberId);
    }

    [Fact]
    public void Compression_member_reports_buckling()
    {
        var doc = Triangle();
        var r = TrussSolver.Solve(doc);
        Assert.True(r.Members[1].IsCompression);
        Assert.True(r.Members[1].UtilizationBuckling > 0);
        Assert.Equal(0, r.Members[3].UtilizationBuckling);
    }

    [Fact]
    public void Detached_part_is_left_out_but_the_rest_still_solves()
    {
        var doc = Triangle();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(10, 0)));
        h.Do(new AddNodeEdit(new Vec2(12, 0)));
        h.Do(new AddMemberEdit(4, 5, Section.Chs48));
        var r = TrussSolver.Solve(doc);
        Assert.Equal(AnalysisStatus.Solved, r.Status);
        Assert.Equal(new[] { 4, 5 }, r.DetachedNodeIds);
        Assert.InRange(r.Members[1].AxialForceKn, -6.02, -6.00);
        Assert.Equal(0, r.Members[4].AxialForceKn);
    }

    [Theory]
    [InlineData("warren")]
    [InlineData("cantilever")]
    [InlineData("roof")]
    public void Every_sample_solves_below_full_utilization(string id)
    {
        var doc = new StructureDocument();
        SampleStructures.Build(id).Restore(doc);
        var r = TrussSolver.Solve(doc);
        Assert.Equal(AnalysisStatus.Solved, r.Status);
        Assert.Empty(r.DetachedNodeIds);
        Assert.InRange(r.MaxUtilization, 0.05, 0.99);
    }

    // ---- several parts, near-singular geometry ----

    /// <summary>The triangle of <see cref="Triangle"/>, again, shifted right: a second, independent part.</summary>
    private static void AddSecondTriangle(StructureDocument doc, SupportKind left, SupportKind right, bool loaded = true)
    {
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(10, 0)));
        h.Do(new AddNodeEdit(new Vec2(14, 0)));
        h.Do(new AddNodeEdit(new Vec2(12, 3)));
        if (left != SupportKind.None) h.Do(new SetSupportEdit(4, left));
        if (right != SupportKind.None) h.Do(new SetSupportEdit(5, right));
        if (loaded) h.Do(new SetLoadEdit(6, new Vec2(0, -10)));
        h.Do(new AddMemberEdit(4, 6, Section.Chs48));
        h.Do(new AddMemberEdit(5, 6, Section.Chs48));
        h.Do(new AddMemberEdit(4, 5, Section.Chs48));
    }

    [Fact]
    public void Two_separately_supported_parts_each_solve_as_if_alone()
    {
        var doc = Triangle();
        AddSecondTriangle(doc, SupportKind.Pin, SupportKind.RollerY);
        var r = TrussSolver.Solve(doc);

        Assert.Equal(AnalysisStatus.Solved, r.Status);
        Assert.Empty(r.DetachedNodeIds);
        for (var offset = 0; offset <= 3; offset += 3)
        {
            Assert.InRange(r.Members[1 + offset].AxialForceKn, -6.02, -6.00);
            Assert.InRange(r.Members[3 + offset].AxialForceKn, 3.32, 3.34);
            Assert.InRange(r.Nodes[1 + offset].Reaction.Y, 4.99, 5.01);
        }
    }

    [Fact]
    public void A_supported_part_with_no_load_carries_nothing_beside_a_loaded_one()
    {
        var doc = Triangle();
        AddSecondTriangle(doc, SupportKind.Pin, SupportKind.RollerY, loaded: false);
        var r = TrussSolver.Solve(doc);

        Assert.Equal(AnalysisStatus.Solved, r.Status);
        Assert.InRange(r.Members[1].AxialForceKn, -6.02, -6.00);
        for (var id = 4; id <= 6; id++) Assert.InRange(r.Members[id].AxialForceKn, -1e-9, 1e-9);
    }

    [Fact]
    public void A_part_held_by_one_roller_is_a_mechanism_even_when_another_part_is_fully_supported()
    {
        // Pin + roller + roller fix four directions in total, which passes the count check;
        // the second part can still slide and tip, so the stiffness matrix is singular.
        var doc = Triangle();
        AddSecondTriangle(doc, SupportKind.RollerY, SupportKind.None);
        Assert.Equal(AnalysisStatus.Mechanism, TrussSolver.Solve(doc).Status);
    }

    /// <summary>Two bars between pins at (0, 0) and (4, 0), meeting at (2, −sag), loaded 10 kN down there.</summary>
    private static StructureDocument Toggle(double sag)
    {
        var doc = new StructureDocument();
        var h = new EditHistory(doc);
        h.Do(new AddNodeEdit(new Vec2(0, 0)));
        h.Do(new AddNodeEdit(new Vec2(4, 0)));
        h.Do(new AddNodeEdit(new Vec2(2, -sag)));
        h.Do(new SetSupportEdit(1, SupportKind.Pin));
        h.Do(new SetSupportEdit(2, SupportKind.Pin));
        h.Do(new SetLoadEdit(3, new Vec2(0, -10)));
        h.Do(new AddMemberEdit(1, 3, Section.Chs48));
        h.Do(new AddMemberEdit(3, 2, Section.Chs48));
        return doc;
    }

    [Fact]
    public void A_shallow_toggle_solves_with_the_forces_statics_gives()
    {
        // Each bar carries P / (2 sin θ): sin θ = 0.2 / √(2² + 0.2²), so 50.25 kN of tension.
        var r = TrussSolver.Solve(Toggle(0.2));
        Assert.Equal(AnalysisStatus.Solved, r.Status);
        var expected = 10 / (2 * (0.2 / Math.Sqrt(4 + 0.04)));
        Assert.InRange(r.Members[1].AxialForceKn, expected - 0.01, expected + 0.01);
        Assert.InRange(r.Members[2].AxialForceKn, expected - 0.01, expected + 0.01);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1e-6)]
    public void A_flat_or_nearly_flat_toggle_is_a_mechanism_not_a_huge_force(double sag)
    {
        // Flat, the middle node has no vertical stiffness at all; a micrometre of sag leaves it a trillionth of the
        // axial stiffness, which the solver's relative pivot test reads as none.
        Assert.Equal(AnalysisStatus.Mechanism, TrussSolver.Solve(Toggle(sag)).Status);
    }
}
