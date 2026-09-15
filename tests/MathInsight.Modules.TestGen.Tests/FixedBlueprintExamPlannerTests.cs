using MathInsight.Modules.TestGen.Blueprints;
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

    [Fact]
    public void PrepareQuestions_MixedSection_UsesTheAssignedDetailScoringRule()
    {
        var blueprint = new Persistence.Entities.Blueprint
        {
            BlueprintId = "mixed-fixed",
            BlueprintName = "Mixed fixed",
            Grade = 12,
            TotalQuestions = 2,
            TotalScore = 10m,
            DurationMinutes = 30,
            ExpertId = "expert-owner",
            Status = BlueprintStatuses.Approved
        };
        var section = new Persistence.Entities.BlueprintSection
        {
            BlueprintSectionId = "mixed-fixed-section",
            BlueprintId = blueprint.BlueprintId,
            SectionOrder = 1,
            SectionName = "Mixed",
            QuestionType = BlueprintQuestionTypes.Mixed,
            ScoringRule = null,
            TotalQuestions = 2,
            ScoreBudget = 10m
        };
        section.Details.Add(new Persistence.Entities.BlueprintDetail
        {
            BlueprintDetailId = "fixed-composite",
            BlueprintId = blueprint.BlueprintId,
            BlueprintSectionId = section.BlueprintSectionId,
            TagId = "topic-composite",
            DifficultyId = "difficulty-1",
            Quantity = 1,
            QuestionType = BlueprintQuestionTypes.Composite,
            ScoringRule = ScoringRules.WeightedParts
        });
        section.Details.Add(new Persistence.Entities.BlueprintDetail
        {
            BlueprintDetailId = "fixed-single",
            BlueprintId = blueprint.BlueprintId,
            BlueprintSectionId = section.BlueprintSectionId,
            TagId = "topic-single",
            DifficultyId = "difficulty-1",
            Quantity = 1,
            QuestionType = BlueprintQuestionTypes.SingleChoice,
            ScoringRule = ScoringRules.AllOrNothing
        });
        blueprint.Sections.Add(section);
        var requirements = BlueprintExamGenerationPlanner.BuildRequirements(blueprint);
        var candidates = new[]
        {
            new BlueprintExamCandidate("fixed-composite-question", "fixed-composite-version", 1m, "difficulty-1",
                BlueprintQuestionTypes.Composite, new HashSet<string>(["topic-composite"]),
                new HashSet<string>([ScoringRules.WeightedParts])),
            new BlueprintExamCandidate("fixed-single-question", "fixed-single-version", 1m, "difficulty-1",
                BlueprintQuestionTypes.SingleChoice, new HashSet<string>(["topic-single"]),
                new HashSet<string>([ScoringRules.AllOrNothing]))
        };
        var selection = FixedBlueprintExamPlanner.Select(
            requirements,
            candidates,
            [
                new("fixed-composite-question", "fixed-composite", 1),
                new("fixed-single-question", "fixed-single", 2)
            ]);

        Assert.Equal(FixedBlueprintExamSelectionError.None, selection.Error);
        var prepared = FixedBlueprintExamPlanner.PrepareQuestions(blueprint, selection.Selection, candidates);

        Assert.Equal(
            new[] { ScoringRules.WeightedParts, ScoringRules.AllOrNothing },
            prepared.OrderBy(item => item.QuestionOrder).Select(item => item.ScoringRule));
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
