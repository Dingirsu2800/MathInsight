using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class ScoringAllocatorTests
{
    [Fact]
    public void Allocate_WeightsOneOnePointFiveAndTwo_PreservesExactRatio()
    {
        var result = ScoringAllocator.Allocate(
            9m,
            [
                new WeightedScoreItem("weight-one", 1m, 1),
                new WeightedScoreItem("weight-one-point-five", 1.5m, 2),
                new WeightedScoreItem("weight-two", 2m, 3)
            ]);

        Assert.Equal(2m, result["weight-one"]);
        Assert.Equal(3m, result["weight-one-point-five"]);
        Assert.Equal(4m, result["weight-two"]);
    }

    [Fact]
    public void Allocate_LargestRemainder_ProducesExactCentTotal()
    {
        var result = ScoringAllocator.Allocate(
            10m,
            [
                new WeightedScoreItem("weight-one", 1m, 1),
                new WeightedScoreItem("weight-one-point-five", 1.5m, 2),
                new WeightedScoreItem("weight-two", 2m, 3)
            ]);

        Assert.Equal(2.22m, result["weight-one"]);
        Assert.Equal(3.33m, result["weight-one-point-five"]);
        Assert.Equal(4.45m, result["weight-two"]);
        Assert.Equal(10m, result.Values.Sum());
    }

    [Fact]
    public void Allocate_EqualRemainders_UsesStableFinalOrder()
    {
        var result = ScoringAllocator.Allocate(
            0.02m,
            [
                new WeightedScoreItem("final", 1m, 3),
                new WeightedScoreItem("first", 1m, 1),
                new WeightedScoreItem("second", 1m, 2)
            ]);

        Assert.Equal(0.01m, result["first"]);
        Assert.Equal(0.01m, result["second"]);
        Assert.Equal(0m, result["final"]);
        Assert.Equal(0.02m, result.Values.Sum());
    }
}
