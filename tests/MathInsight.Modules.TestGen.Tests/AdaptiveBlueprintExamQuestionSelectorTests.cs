using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Generation;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class AdaptiveBlueprintExamQuestionSelectorTests
{
    private const string OriginalDifficulty = "difficulty-original";
    private const string PreferredDifficulty = "difficulty-preferred";

    [Fact]
    public void Select_PrefersAdjustedDifficultyWhenBothLevelsAreAvailable()
    {
        var requirement = Requirement("detail-a", "topic-a", OriginalDifficulty);
        var plans = Plans(requirement, PreferredDifficulty);
        var candidates = new[]
        {
            Candidate("original", "topic-a", OriginalDifficulty),
            Candidate("preferred", "topic-a", PreferredDifficulty)
        };

        var result = CreateSelector().Select([requirement], plans, candidates, CancellationToken.None);

        var assignment = Assert.Single(result.Assignments);
        Assert.True(result.IsComplete);
        Assert.Equal("preferred", assignment.QuestionId);
    }

    [Fact]
    public void Select_FallsBackToOriginalDifficultyWhenPreferredPoolIsShort()
    {
        var requirement = Requirement("detail-a", "topic-a", OriginalDifficulty);
        var plans = Plans(requirement, PreferredDifficulty);

        var result = CreateSelector().Select(
            [requirement],
            plans,
            [Candidate("original", "topic-a", OriginalDifficulty)],
            CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Equal("original", Assert.Single(result.Assignments).QuestionId);
    }

    [Fact]
    public void Select_RejectsCandidatesAtUnrelatedDifficulty()
    {
        var requirement = Requirement("detail-a", "topic-a", OriginalDifficulty);
        var plans = Plans(requirement, PreferredDifficulty);

        var result = CreateSelector().Select(
            [requirement],
            plans,
            [Candidate("unrelated", "topic-a", "difficulty-unrelated")],
            CancellationToken.None);

        Assert.False(result.IsComplete);
        Assert.Empty(result.Assignments);
    }

    [Fact]
    public void Select_UsesEachQuestionAtMostOnceAcrossOverlappingTopics()
    {
        var first = Requirement("detail-a", "topic-a", OriginalDifficulty);
        var second = Requirement("detail-b", "topic-b", OriginalDifficulty);
        var plans = Plans(first, OriginalDifficulty, second, OriginalDifficulty);

        var result = CreateSelector().Select(
            [first, second],
            plans,
            [Candidate("shared", "topic-a", OriginalDifficulty, "topic-b"), Candidate("topic-a-only", "topic-a", OriginalDifficulty)],
            CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Assignments.Count);
        Assert.Equal(2, result.Assignments.Select(item => item.QuestionId).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains(result.Assignments, item => item.QuestionId == "shared" && item.BlueprintDetailId == "detail-b");
        Assert.Contains(result.Assignments, item => item.QuestionId == "topic-a-only" && item.BlueprintDetailId == "detail-a");
    }

    [Fact]
    public void Select_FindsCompleteAssignmentWhenGreedyChoiceWouldBlockAnotherRequirement()
    {
        var first = Requirement("detail-a", "topic-a", OriginalDifficulty);
        var second = Requirement("detail-b", "topic-b", OriginalDifficulty);
        var plans = Plans(first, PreferredDifficulty, second, OriginalDifficulty);
        var candidates = new[]
        {
            Candidate("flexible-original", "topic-a", OriginalDifficulty, "topic-b"),
            Candidate("preferred-a", "topic-a", PreferredDifficulty)
        };

        var result = CreateSelector().Select([first, second], plans, candidates, CancellationToken.None);

        Assert.True(result.IsComplete);
        Assert.Contains(result.Assignments, item => item.QuestionId == "preferred-a" && item.BlueprintDetailId == "detail-a");
        Assert.Contains(result.Assignments, item => item.QuestionId == "flexible-original" && item.BlueprintDetailId == "detail-b");
    }

    [Fact]
    public void Select_HonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => CreateSelector().Select(
            [Requirement("detail-a", "topic-a", OriginalDifficulty)],
            Plans(Requirement("detail-a", "topic-a", PreferredDifficulty), PreferredDifficulty),
            [Candidate("question", "topic-a", OriginalDifficulty)],
            cancellation.Token));
    }

    private static IAdaptiveBlueprintExamQuestionSelector CreateSelector()
        => new AdaptiveBlueprintExamQuestionSelector(new NoOpGenerationRandomizer());

    private static BlueprintExamRequirement Requirement(string detailId, string tagId, string difficultyId)
        => new(detailId, 1, 1, tagId, difficultyId, BlueprintQuestionTypes.SingleChoice, "AllOrNothing", 1);

    private static Dictionary<string, AdaptiveBlueprintDetailPlan> Plans(
        BlueprintExamRequirement requirement,
        string preferredDifficultyId,
        BlueprintExamRequirement? secondRequirement = null,
        string? secondPreferredDifficultyId = null)
    {
        var plans = new Dictionary<string, AdaptiveBlueprintDetailPlan>(StringComparer.OrdinalIgnoreCase)
        {
            [requirement.BlueprintDetailId] = new(
                requirement.BlueprintDetailId,
                requirement.TagId,
                requirement.DifficultyId,
                preferredDifficultyId,
                4m,
                true,
                !string.Equals(requirement.DifficultyId, preferredDifficultyId, StringComparison.OrdinalIgnoreCase))
        };

        if (secondRequirement is not null)
        {
            plans[secondRequirement.BlueprintDetailId] = new(
                secondRequirement.BlueprintDetailId,
                secondRequirement.TagId,
                secondRequirement.DifficultyId,
                secondPreferredDifficultyId ?? secondRequirement.DifficultyId,
                null,
                false,
                false);
        }

        return plans;
    }

    private static BlueprintExamCandidate Candidate(
        string questionId,
        string firstTag,
        string difficultyId,
        params string[] additionalTags)
        => new(
            questionId,
            $"{questionId}-version",
            1m,
            difficultyId,
            BlueprintQuestionTypes.SingleChoice,
            additionalTags.Append(firstTag).ToHashSet(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(["AllOrNothing"], StringComparer.OrdinalIgnoreCase));

    private sealed class NoOpGenerationRandomizer : IGenerationRandomizer
    {
        public void Shuffle<T>(IList<T> values)
        {
        }
    }
}
