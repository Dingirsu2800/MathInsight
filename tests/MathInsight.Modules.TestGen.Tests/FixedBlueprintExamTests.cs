using System.Text.Json;
using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.GenerateFixedBlueprintExam;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Modules.TestGen.Queries.GetFixedTestCandidates;
using MathInsight.Modules.TestGen.Queries.GetBlueprintGeneratedTests;
using MathInsight.Shared.Questions;
using MathInsight.Shared.Scoring;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class FixedBlueprintExamTests
{
    [Fact]
    public async Task Candidates_ReturnOnlyQuestionsEligibleForRequestedDetail()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddBlueprint(fixture, BlueprintStatuses.Active, 1);
        var eligible = Candidate("question-1");
        var wrongDifficulty = Candidate("question-2") with { DifficultyId = "difficulty-2" };
        fixture.Context.QuestionVersions.Add(new QuestionVersionReadModel
        {
            VersionId = eligible.QuestionVersionId,
            QuestionId = eligible.QuestionId,
            VersionNumber = 1,
            SnapshotSchemaVersion = 2,
            AnswersSnapshot = JsonSerializer.Serialize(new QuestionSnapshotV2(
                eligible.QuestionId, "SingleChoice", "difficulty-1", 12, 1m,
                [new QuestionTopicSnapshot("topic-1", true)],
                [new QuestionAnswerSnapshot("answer-1", "A", true)], [],
                "Derivative prompt", "Solution")),
            CreatedTime = DateTime.UtcNow
        });
        await fixture.Context.SaveChangesAsync();

        var result = await new GetFixedTestCandidatesQueryHandler(
            fixture.Context, new StubCandidateProvider([eligible, wrongDifficulty])).Handle(
                new("blueprint-1", "expert-1", "detail-1", "Derivative", 1, 20),
                CancellationToken.None);

        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("question-1", item.QuestionId);
        Assert.Equal("topic-1", item.TagId);
        Assert.Contains(ScoringRules.AllOrNothing, item.SupportedScoringRules);
    }

    [Fact]
    public async Task Generate_ValidSelection_PersistsExactOrderAndFixedMetadata()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(fixture, BlueprintStatuses.Approved, 2);
        fixture.Context.Experts.Add(new ExpertReadModel { ExpertId = "expert-1" });
        await fixture.Context.SaveChangesAsync();
        var handler = Handler(fixture, Candidate("question-1"), Candidate("question-2"));

        var result = await handler.Handle(new(
            "blueprint-1", "expert-1", "Official mock 101", 50,
            [
                new FixedBlueprintExamQuestionRequest { QuestionId = "question-2", BlueprintDetailId = "detail-1", QuestionOrder = 1 },
                new FixedBlueprintExamQuestionRequest { QuestionId = "question-1", BlueprintDetailId = "detail-1", QuestionOrder = 2 }
            ]), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(GeneratedTestValues.ExpertGenerator, result.Value!.GeneratedBy);
        Assert.Equal(BlueprintStatuses.Active, blueprint.Status);
        var test = await fixture.Context.Tests.Include(x => x.Questions).SingleAsync();
        Assert.Equal(["question-2", "question-1"], test.Questions.OrderBy(x => x.QuestionOrder).Select(x => x.QuestionId));
        Assert.All(test.Questions, x => Assert.Equal(GeneratedTestValues.FixedExamReason, x.SelectionReason));
        Assert.Equal(10m, test.Questions.Sum(x => x.MaxPointsSnapshot));

        var list = await new GetBlueprintGeneratedTestsQueryHandler(fixture.Context).Handle(
            new("blueprint-1", "expert-1", 1, 20), CancellationToken.None);
        Assert.Equal(GeneratedTestValues.FixedGenerationType, Assert.Single(list.Value!.Items).GenerationType);
    }

    [Fact]
    public async Task Generate_DuplicateQuestion_ReturnsStableErrorWithoutWritingTest()
    {
        await using var fixture = TestGenInMemoryContext.Create();
        AddBlueprint(fixture, BlueprintStatuses.Approved, 2);
        fixture.Context.Experts.Add(new ExpertReadModel { ExpertId = "expert-1" });
        await fixture.Context.SaveChangesAsync();

        var result = await Handler(fixture, Candidate("question-1")).Handle(new(
            "blueprint-1", "expert-1", "Invalid", 50,
            [
                new FixedBlueprintExamQuestionRequest { QuestionId = "question-1", BlueprintDetailId = "detail-1", QuestionOrder = 1 },
                new FixedBlueprintExamQuestionRequest { QuestionId = "question-1", BlueprintDetailId = "detail-1", QuestionOrder = 2 }
            ]), CancellationToken.None);

        Assert.Equal(TestGenerationErrors.FixedTestQuestionDuplicated, result.Error);
        Assert.Empty(fixture.Context.Tests);
    }

    private static GenerateFixedBlueprintExamCommandHandler Handler(
        TestGenInMemoryContext fixture,
        params BlueprintExamCandidate[] candidates)
        => new(fixture.Context, new StubCandidateProvider(candidates), new StubCodeGenerator());

    private static Blueprint AddBlueprint(TestGenInMemoryContext fixture, string status, int quantity)
    {
        var blueprint = new Blueprint
        {
            BlueprintId = "blueprint-1",
            BlueprintName = "Blueprint",
            Grade = 12,
            TotalQuestions = quantity,
            TotalScore = 10m,
            DurationMinutes = 50,
            ExpertId = "expert-1",
            Status = status
        };
        var section = new BlueprintSection
        {
            BlueprintSectionId = "section-1",
            BlueprintId = blueprint.BlueprintId,
            SectionOrder = 1,
            SectionName = "Section",
            QuestionType = "SingleChoice",
            TotalQuestions = quantity,
            ScoreBudget = 10m,
            ScoringRule = ScoringRules.AllOrNothing
        };
        section.Details.Add(new BlueprintDetail
        {
            BlueprintDetailId = "detail-1",
            BlueprintId = blueprint.BlueprintId,
            BlueprintSectionId = section.BlueprintSectionId,
            TagId = "topic-1",
            DifficultyId = "difficulty-1",
            Quantity = quantity
        });
        blueprint.Sections.Add(section);
        fixture.Context.Blueprints.Add(blueprint);
        return blueprint;
    }

    private static BlueprintExamCandidate Candidate(string id) => new(
        id, $"{id}-version", 1m, "difficulty-1", "SingleChoice",
        new HashSet<string>(["topic-1"]), new HashSet<string>([ScoringRules.AllOrNothing]));

    private sealed class StubCandidateProvider(IReadOnlyList<BlueprintExamCandidate> candidates)
        : IBlueprintExamCandidateProvider
    {
        public Task<BlueprintExamCandidatePool> GetCandidatesAsync(Blueprint blueprint, CancellationToken cancellationToken)
            => Task.FromResult(new BlueprintExamCandidatePool(candidates, []));
    }

    private sealed class StubCodeGenerator : ITestCodeGenerator
    {
        public string Generate() => "FIXED101";
    }
}
