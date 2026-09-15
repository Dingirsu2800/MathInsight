using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Validation;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Shared.Scoring;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintAvailabilityTests
{
    [Fact]
    public async Task Preview_UsesDistinctCandidatesAndReturnsPerRowCounts()
    {
        var candidates = new[]
        {
            Candidate("q-1", "topic-a", "difficulty-1", BlueprintQuestionTypes.SingleChoice),
            Candidate("q-2", "topic-a", "difficulty-1", BlueprintQuestionTypes.SingleChoice),
            Candidate("q-2", "topic-a", "difficulty-1", BlueprintQuestionTypes.SingleChoice)
        };
        var checker = CreateChecker(candidates);

        var result = await checker.PreviewAsync(Request(
            Row("row-a", "topic-a", "difficulty-1", 2)), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WholeBlueprintFeasible);
        var row = Assert.Single(result.Value.Sections).Rows.Single();
        Assert.Equal("row-a", row.ClientRowId);
        Assert.Equal(2, row.AvailableCount);
        Assert.Equal(2, row.RequiredCount);
        Assert.Equal(0, row.Shortage);
    }

    [Fact]
    public async Task Preview_DetectsOverlapWhenEveryRowIsIndividuallySufficient()
    {
        var candidates = new[]
        {
            Candidate("shared", "topic-a", "difficulty-1", BlueprintQuestionTypes.SingleChoice),
            Candidate("other", "topic-a", "difficulty-1", BlueprintQuestionTypes.SingleChoice)
        };
        var checker = CreateChecker(candidates);
        var request = Request(
            Row("row-a", "topic-a", "difficulty-1", 2),
            Row("row-b", "topic-a", "difficulty-1", 2));

        var result = await checker.PreviewAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.WholeBlueprintFeasible);
        Assert.Equal(BlueprintAvailabilityCodes.OverlapConflict, result.Value.AvailabilityCode);
        Assert.All(result.Value.Sections.SelectMany(section => section.Rows), row => Assert.Equal(0, row.Shortage));
    }

    [Fact]
    public async Task Preview_DistinguishesShortageFromOverlap()
    {
        var checker = CreateChecker([
            Candidate("only", "topic-a", "difficulty-1", BlueprintQuestionTypes.SingleChoice)
        ]);

        var result = await checker.PreviewAsync(
            Request(Row("row-a", "topic-a", "difficulty-1", 2)),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.WholeBlueprintFeasible);
        Assert.Equal(BlueprintAvailabilityCodes.InsufficientQuestions, result.Value.AvailabilityCode);
        Assert.Equal(1, Assert.Single(Assert.Single(result.Value.Sections).Rows).Shortage);
    }

    [Fact]
    public async Task Preview_MixedRowsCanBeFeasibleWithDifferentQuestionTypes()
    {
        var checker = CreateChecker([
            Candidate("single", "topic-a", "difficulty-1", BlueprintQuestionTypes.SingleChoice),
            Candidate("short", "topic-a", "difficulty-1", BlueprintQuestionTypes.ShortAnswer)
        ]);
        var request = new BlueprintAvailabilityRequest
        {
            Grade = 12,
            Sections =
            [
                new BlueprintAvailabilitySectionRequest
                {
                    ClientSectionId = "mixed-section",
                    QuestionType = BlueprintQuestionTypes.Mixed,
                    Rows =
                    [
                        new BlueprintAvailabilityRowRequest
                        {
                            ClientRowId = "single-row",
                            TagId = "topic-a",
                            DifficultyId = "difficulty-1",
                            Quantity = 1,
                            QuestionType = BlueprintQuestionTypes.SingleChoice,
                            ScoringRule = ScoringRules.AllOrNothing
                        },
                        new BlueprintAvailabilityRowRequest
                        {
                            ClientRowId = "short-row",
                            TagId = "topic-a",
                            DifficultyId = "difficulty-1",
                            Quantity = 1,
                            QuestionType = BlueprintQuestionTypes.ShortAnswer,
                            ScoringRule = ScoringRules.AllOrNothing
                        }
                    ]
                }
            ]
        };

        var result = await checker.PreviewAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WholeBlueprintFeasible);
        Assert.Null(result.Value.AvailabilityCode);
    }

    [Fact]
    public async Task Preview_ExcludesCompositeWithIncompatibleScoringRule()
    {
        var checker = CreateChecker([
            new BlueprintExamCandidate(
                "composite",
                "version-composite",
                1m,
                "difficulty-1",
                BlueprintQuestionTypes.Composite,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "topic-a" },
                new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ScoringRules.WeightedParts })
        ]);
        var request = new BlueprintAvailabilityRequest
        {
            Grade = 12,
            Sections =
            [
                new BlueprintAvailabilitySectionRequest
                {
                    ClientSectionId = "composite-section",
                    QuestionType = BlueprintQuestionTypes.Mixed,
                    Rows =
                    [
                        new BlueprintAvailabilityRowRequest
                        {
                            ClientRowId = "composite-row",
                            TagId = "topic-a",
                            DifficultyId = "difficulty-1",
                            Quantity = 1,
                            QuestionType = BlueprintQuestionTypes.Composite,
                            ScoringRule = ScoringRules.TieredTrueFalse
                        }
                    ]
                }
            ]
        };

        var result = await checker.PreviewAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(Assert.Single(result.Value!.Sections).Rows);
        Assert.Equal(0, row.AvailableCount);
        Assert.Equal(1, row.Shortage);
    }

    [Fact]
    public async Task Preview_RejectsInvalidShapeInsteadOfReturningZeroCapacity()
    {
        var checker = CreateChecker([]);
        var request = Request(Row("", "topic-a", "difficulty-1", 1));

        var result = await checker.PreviewAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.AvailabilityRequestInvalid, result.Error);
    }

    [Fact]
    public async Task Preview_RejectsQuantityOutsideRequestBounds()
    {
        var checker = CreateChecker([]);

        var result = await checker.PreviewAsync(
            Request(Row("row-a", "topic-a", "difficulty-1", 101)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.AvailabilityRequestInvalid, result.Error);
    }

    private static BlueprintAvailabilityChecker CreateChecker(IReadOnlyList<BlueprintExamCandidate> candidates)
        => new(
            new FakeCandidateProvider(candidates),
            new CapacityAwareQuestionSelector(new SystemGenerationRandomizer()),
            new StubBlueprintAggregateValidator());

    private static BlueprintAvailabilityRequest Request(params BlueprintAvailabilityRowRequest[] rows)
        => new()
        {
            Grade = 12,
            Sections =
            [
                new BlueprintAvailabilitySectionRequest
                {
                    ClientSectionId = "section-a",
                    QuestionType = BlueprintQuestionTypes.SingleChoice,
                    ScoringRule = ScoringRules.AllOrNothing,
                    Rows = rows.ToList()
                }
            ]
        };

    private static BlueprintAvailabilityRowRequest Row(string id, string topic, string difficulty, int quantity)
        => new()
        {
            ClientRowId = id,
            TagId = topic,
            DifficultyId = difficulty,
            Quantity = quantity
        };

    private static BlueprintExamCandidate Candidate(string id, string topic, string difficulty, string type)
        => new(id, $"version-{id}", 1m, difficulty, type,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { topic },
            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ScoringRules.AllOrNothing });

    private sealed class FakeCandidateProvider : IBlueprintExamCandidateProvider
    {
        private readonly IReadOnlyList<BlueprintExamCandidate> _candidates;

        public FakeCandidateProvider(IReadOnlyList<BlueprintExamCandidate> candidates) => _candidates = candidates;

        public Task<BlueprintExamCandidatePool> GetCandidatesAsync(Blueprint blueprint, CancellationToken cancellationToken)
            => Task.FromResult(new BlueprintExamCandidatePool(_candidates, []));
    }

    private sealed class StubBlueprintAggregateValidator : IBlueprintAggregateValidator
    {
        public Task<MathInsight.Shared.Results.Result<ValidatedBlueprintAggregate>> ValidateAsync(
            BlueprintRequest request,
            CancellationToken cancellationToken)
            => Task.FromResult(MathInsight.Shared.Results.Result<ValidatedBlueprintAggregate>.Success(null!));
    }
}
