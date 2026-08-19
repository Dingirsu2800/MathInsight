using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Generation;
using MathInsight.Modules.TestGen.Persistence.Entities;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintExamCandidateProviderTests
{
    [Fact]
    public async Task GetCandidatesAsync_AdaptiveOverloadUsesUnionOfOriginalAndPreferredDifficulties()
    {
        var catalog = new CapturingCatalog();
        var provider = new BlueprintExamCandidateProvider(catalog);
        var blueprint = CreateBlueprint();

        await provider.GetCandidatesAsync(
            blueprint,
            ["difficulty-original", "difficulty-preferred"],
            CancellationToken.None);

        Assert.NotNull(catalog.Filter);
        Assert.Equal(12, catalog.Filter!.Grade);
        Assert.Equal(["topic-a"], catalog.Filter.TagIds);
        Assert.Equal(["difficulty-original", "difficulty-preferred"], catalog.Filter.DifficultyIds);
        Assert.Equal([BlueprintQuestionTypes.SingleChoice], catalog.Filter.QuestionTypes);
    }

    [Fact]
    public async Task GetCandidatesAsync_BaselineOverloadKeepsOriginalDifficultyFilter()
    {
        var catalog = new CapturingCatalog();
        var provider = new BlueprintExamCandidateProvider(catalog);
        var blueprint = CreateBlueprint();

        await provider.GetCandidatesAsync(blueprint, CancellationToken.None);

        Assert.NotNull(catalog.Filter);
        Assert.Equal(["difficulty-original"], catalog.Filter!.DifficultyIds);
    }

    private static Blueprint CreateBlueprint()
    {
        var blueprint = new Blueprint
        {
            BlueprintId = "blueprint",
            BlueprintName = "Blueprint",
            Grade = 12,
            TotalQuestions = 1,
            TotalScore = 1m,
            DurationMinutes = 30,
            Status = BlueprintStatuses.Approved
        };
        var section = new BlueprintSection
        {
            BlueprintSectionId = "section",
            BlueprintId = blueprint.BlueprintId,
            SectionOrder = 1,
            SectionName = "Single choice",
            QuestionType = BlueprintQuestionTypes.SingleChoice,
            ScoringRule = "AllOrNothing",
            TotalQuestions = 1,
            ScoreBudget = 1m
        };
        section.Details.Add(new BlueprintDetail
        {
            BlueprintDetailId = "detail",
            BlueprintId = blueprint.BlueprintId,
            BlueprintSectionId = section.BlueprintSectionId,
            TagId = "topic-a",
            DifficultyId = "difficulty-original",
            Quantity = 1
        });
        blueprint.Sections.Add(section);
        return blueprint;
    }

    private sealed class CapturingCatalog : IQuestionCandidateCatalog
    {
        public QuestionCandidateCatalogFilter? Filter { get; private set; }

        public Task<BlueprintExamCandidatePool> GetCandidatesAsync(
            QuestionCandidateCatalogFilter filter,
            CancellationToken cancellationToken)
        {
            Filter = filter;
            return Task.FromResult(new BlueprintExamCandidatePool([], []));
        }
    }
}
