using MathInsight.Modules.TestGen.Generation;
using MathInsight.Shared.Recommendations;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class AdaptiveBlueprintExamPolicyTests
{
    [Theory]
    [InlineData(0, 3, 5, 2)]
    [InlineData(4.99, 3, 5, 2)]
    [InlineData(5, 3, 5, 3)]
    [InlineData(7.49, 3, 5, 3)]
    [InlineData(7.5, 3, 5, 4)]
    [InlineData(10, 3, 5, 4)]
    public void ResolvePreferredLevel_UsesOfficialPointBoundaries(
        double point,
        int originalLevel,
        int evidenceCount,
        int expectedLevel)
    {
        var mastery = new TopicMasteryAdvice("topic", (decimal)point, evidenceCount, 2, 2);

        var result = AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(originalLevel, mastery);

        Assert.Equal(expectedLevel, result);
    }

    [Theory]
    [InlineData(2)]
    public void ResolvePreferredLevel_KeepsOriginalWhenEvidenceIsInsufficient(int evidenceCount)
    {
        var mastery = new TopicMasteryAdvice("topic", 1m, evidenceCount, 0, 1);

        var result = AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(3, mastery);

        Assert.Equal(3, result);
    }

    [Fact]
    public void ResolvePreferredLevel_KeepsOriginalWhenMasteryIsMissing()
    {
        var result = AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(2, null);

        Assert.Equal(2, result);
    }

    [Fact]
    public void ResolvePreferredLevel_ClampsWeakLevelOneAndStrongLevelFour()
    {
        var weak = new TopicMasteryAdvice("topic", 4.99m, 3, 2, 1);
        var strong = new TopicMasteryAdvice("topic", 7.5m, 3, 2, 4);

        Assert.Equal(1, AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(1, weak));
        Assert.Equal(4, AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(4, strong));
    }
}
