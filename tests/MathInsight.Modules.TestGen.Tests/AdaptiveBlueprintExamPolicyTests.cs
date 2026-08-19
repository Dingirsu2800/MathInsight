using MathInsight.Modules.TestGen.Generation;
using MathInsight.Shared.Recommendations;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class AdaptiveBlueprintExamPolicyTests
{
    [Theory]
    [InlineData(4, 2, 1, 3, 3)]
    [InlineData(5, 1, 1, 3, 3)]
    [InlineData(5, 2, 1, 3, 2)]
    [InlineData(7, 3, 1, 3, 2)]
    [InlineData(8, 2, 1, 3, 2)]
    [InlineData(8, 3, 1, 3, 1)]
    public void ResolvePreferredLevel_UsesItemAndSessionQualification(
        int evidenceItemCount,
        int evidenceSessionCount,
        double point,
        int originalLevel,
        int expectedLevel)
    {
        var mastery = new TopicMasteryAdvice(
            "topic",
            (decimal)point,
            evidenceItemCount,
            evidenceSessionCount,
            2);

        var result = AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(originalLevel, mastery);

        Assert.Equal(expectedLevel, result);
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(5, 3)]
    [InlineData(7.5, 4)]
    public void ResolvePreferredLevel_UsesPointBoundaries(
        double point,
        int expectedLevel)
    {
        var mastery = new TopicMasteryAdvice("topic", (decimal)point, 5, 2, 2);

        Assert.Equal(expectedLevel,
            AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(3, mastery));
    }

    [Fact]
    public void ResolvePreferredLevel_ClampsStrongWeakAndStrongHighLevels()
    {
        var weak = new TopicMasteryAdvice("topic", 1m, 8, 3, 1);
        var strong = new TopicMasteryAdvice("topic", 8m, 8, 3, 4);

        Assert.Equal(1, AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(1, weak));
        Assert.Equal(4, AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(4, strong));
    }

    [Fact]
    public void MissingMasteryKeepsOriginalLevel()
    {
        Assert.Equal(2, AdaptiveBlueprintExamPolicy.ResolvePreferredLevel(2, null));
    }
}
