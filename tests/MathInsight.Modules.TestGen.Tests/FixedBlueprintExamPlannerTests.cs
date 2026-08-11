using MathInsight.Modules.TestGen.Generation;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class FixedBlueprintExamPlannerTests
{
    private static readonly BlueprintExamRequirement Requirement = new(
        "detail-1", 1, 0, "topic-1", "difficulty-1", "SingleChoice",
        ScoringRules.AllOrNothing, 2);

    [Fact]
    public void Select_ValidExplicitQuestions_PreservesExpertOrder()
    {
        var candidates = new[] { Candidate("question-1"), Candidate("question-2") };
        var requested = new[]
        {
            new FixedBlueprintExamQuestionSelection("question-2", "detail-1", 1),
            new FixedBlueprintExamQuestionSelection("question-1", "detail-1", 2)
        };

        var result = FixedBlueprintExamPlanner.Select([Requirement], candidates, requested);

        Assert.Equal(FixedBlueprintExamSelectionError.None, result.Error);
        Assert.Equal(["question-2", "question-1"], result.Selection.Assignments.Select(x => x.QuestionId));
    }

    [Theory]
    [InlineData(true, false, FixedBlueprintExamSelectionError.DuplicateQuestion)]
    [InlineData(false, true, FixedBlueprintExamSelectionError.InvalidOrder)]
    public void Select_InvalidIdentityOrOrder_ReturnsStableError(
        bool duplicateQuestion,
        bool duplicateOrder,
        FixedBlueprintExamSelectionError expected)
    {
        var requested = new[]
        {
            new FixedBlueprintExamQuestionSelection("question-1", "detail-1", 1),
            new FixedBlueprintExamQuestionSelection(duplicateQuestion ? "question-1" : "question-2", "detail-1", duplicateOrder ? 1 : 2)
        };

        var result = FixedBlueprintExamPlanner.Select(
            [Requirement],
            [Candidate("question-1"), Candidate("question-2")],
            requested);

        Assert.Equal(expected, result.Error);
    }

    [Fact]
    public void Select_QuestionDoesNotMatchAssignedDetail_ReturnsNotEligible()
    {
        var wrongDifficulty = Candidate("question-2") with { DifficultyId = "difficulty-2" };

        var result = FixedBlueprintExamPlanner.Select(
            [Requirement],
            [Candidate("question-1"), wrongDifficulty],
            [
                new("question-1", "detail-1", 1),
                new("question-2", "detail-1", 2)
            ]);

        Assert.Equal(FixedBlueprintExamSelectionError.QuestionNotEligible, result.Error);
    }

    private static BlueprintExamCandidate Candidate(string questionId)
        => new(
            questionId,
            $"{questionId}-version",
            1m,
            "difficulty-1",
            "SingleChoice",
            new HashSet<string>(["topic-1"]),
            new HashSet<string>([ScoringRules.AllOrNothing]));
}
