using Loadpath.Core.Analysis;
using Xunit;

namespace Loadpath.Tests;

public class LinearSolverTests
{
    [Fact]
    public void Solves_a_system_that_needs_a_row_swap()
    {
        // A zero first pivot: plain elimination would divide by zero, partial pivoting swaps rows first.
        var a = new double[,] { { 0, 2, 1 }, { 1, 1, 1 }, { 2, 1, 3 } };
        var b = new double[] { 7, 6, 13 };

        Assert.True(LinearSolver.TrySolve(a, b, out var x));

        Assert.Equal(1, x[0], 12);
        Assert.Equal(2, x[1], 12);
        Assert.Equal(3, x[2], 12);
    }

    [Fact]
    public void An_empty_system_solves_to_nothing()
    {
        Assert.True(LinearSolver.TrySolve(new double[0, 0], [], out var x));
        Assert.Empty(x);
    }

    [Theory]
    [InlineData(1, 2, 2, 4)]
    [InlineData(0, 0, 0, 0)]
    [InlineData(3, 0, 6, 0)]
    public void A_singular_matrix_is_refused(double a00, double a01, double a10, double a11)
    {
        Assert.False(LinearSolver.TrySolve(new[,] { { a00, a01 }, { a10, a11 } }, [1, 1], out _));
    }

    [Theory]
    [InlineData(1e-12)]
    [InlineData(1)]
    [InlineData(1e12)]
    public void The_singularity_test_is_relative_to_the_matrix_so_units_do_not_matter(double scale)
    {
        // Well conditioned at every scale.
        Assert.True(LinearSolver.TrySolve(new[,] { { 4 * scale, 1 * scale }, { 1 * scale, 3 * scale } }, [5 * scale, 4 * scale], out var x));
        Assert.Equal(1, x[0], 9);
        Assert.Equal(1, x[1], 9);

        // A pivot a trillion times smaller than the largest entry reads as zero at every scale.
        Assert.False(LinearSolver.TrySolve(new[,] { { scale, 0 }, { 0, 1e-12 * scale } }, [1, 1], out _));
    }

    [Fact]
    public void A_small_but_real_pivot_still_solves()
    {
        Assert.True(LinearSolver.TrySolve(new[,] { { 1, 0 }, { 0, 1e-6 } }, [1, 2e-6], out var x));
        Assert.Equal(1, x[0], 12);
        Assert.Equal(2, x[1], 9);
    }
}
