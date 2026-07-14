using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.CreateBlueprint;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Modules.TestGen.Queries.GetBlueprintDetail;
using MathInsight.Modules.TestGen.Queries.GetBlueprintList;
using MathInsight.Modules.TestGen.Queries.GetPendingBlueprints;
using MathInsight.Modules.TestGen.Validation;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintCreateAndReadTests
{
    private const string CurrentExpertId = "expert-current";
    private const string OtherExpertId = "expert-other";
    private const string Grade12TopicId = "topic-grade-12";
    private const string Grade11TopicId = "topic-grade-11";
    private const string InactiveTopicId = "topic-inactive";
    private const string EasyDifficultyId = "difficulty-easy";
    private const string InactiveDifficultyId = "difficulty-inactive";

    [Fact]
    public async Task Create_ValidAggregate_PersistsOwnedNormalizedDraft()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        var handler = CreateHandler(testContext);
        var request = ValidRequest();
        request.BlueprintName = "  Blueprint THPT  ";
        request.Sections[0].QuestionType = "single_choice";

        var result = await handler.Handle(
            new CreateBlueprintCommand(request, CurrentExpertId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BlueprintStatuses.Draft, result.Value!.Status);
        Assert.Equal(36, result.Value.BlueprintId.Length);

        var blueprint = await testContext.Context.Blueprints
            .Include(item => item.Sections)
            .ThenInclude(section => section.Details)
            .SingleAsync();

        Assert.Equal(CurrentExpertId, blueprint.ExpertId);
        Assert.Equal("Blueprint THPT", blueprint.BlueprintName);
        Assert.Equal(BlueprintQuestionTypes.SingleChoice, blueprint.Sections.Single().QuestionType);
        Assert.All(blueprint.Sections, section => Assert.Equal(36, section.BlueprintSectionId.Length));
        Assert.All(
            blueprint.Sections.SelectMany(section => section.Details),
            detail =>
            {
                Assert.Equal(36, detail.BlueprintDetailId.Length);
                Assert.Equal(blueprint.BlueprintId, detail.BlueprintId);
            });
    }

    [Fact]
    public async Task Create_UnknownExpert_ReturnsInvalidToken()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        var handler = CreateHandler(testContext);

        var result = await handler.Handle(
            new CreateBlueprintCommand(ValidRequest(), "missing-expert"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_INVALID_TOKEN", result.Error!.Code);
        Assert.Empty(testContext.Context.Blueprints);
    }

    [Fact]
    public async Task Validator_InvalidCompositeMetadata_ReturnsStructureInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        var request = ValidRequest();
        request.Sections[0].QuestionType = BlueprintQuestionTypes.Composite;
        request.Sections[0].PartCountPerQuestion = null;
        request.Sections[0].DefaultPointPerPart = null;

        var result = await CreateValidator(testContext).ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.StructureInvalid, result.Error);
    }

    [Fact]
    public async Task Validator_DuplicateDetailSlot_ReturnsStructureInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        var request = ValidRequest();
        request.Sections[0].Details.Add(new BlueprintDetailRequest
        {
            TagId = Grade12TopicId.ToUpperInvariant(),
            DifficultyId = EasyDifficultyId.ToUpperInvariant(),
            Quantity = 1
        });

        var result = await CreateValidator(testContext).ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.StructureInvalid, result.Error);
    }

    [Fact]
    public async Task Validator_PointWithMoreThanTwoDecimals_ReturnsStructureInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        var request = ValidRequest();
        request.Sections[0].DefaultPointPerQuestion = 0.125m;

        var result = await CreateValidator(testContext).ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.StructureInvalid, result.Error);
    }

    [Theory]
    [InlineData(Grade11TopicId, EasyDifficultyId)]
    [InlineData(InactiveTopicId, EasyDifficultyId)]
    [InlineData(Grade12TopicId, InactiveDifficultyId)]
    public async Task Validator_InvalidTaxonomy_ReturnsTaxonomyInvalid(string topicId, string difficultyId)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        var request = ValidRequest();
        request.Sections[0].Details[0].TagId = topicId;
        request.Sections[0].Details[0].DifficultyId = difficultyId;

        var result = await CreateValidator(testContext).ValidateAsync(request, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.TaxonomyInvalid, result.Error);
    }

    [Fact]
    public async Task List_IncludeDeactivated_OnlyRevealsCurrentExpertsHiddenRows()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        AddBlueprint(testContext, "active-b", "B Blueprint", OtherExpertId, BlueprintStatuses.Draft, sectionCount: 1);
        AddBlueprint(testContext, "active-a", "A Blueprint", OtherExpertId, BlueprintStatuses.Approved, sectionCount: 2);
        AddBlueprint(testContext, "own-hidden", "Own Hidden", CurrentExpertId, BlueprintStatuses.Deactivated, sectionCount: 1);
        AddBlueprint(testContext, "other-hidden", "Other Hidden", OtherExpertId, BlueprintStatuses.Deactivated, sectionCount: 1);
        await testContext.Context.SaveChangesAsync();
        var handler = new GetBlueprintListQueryHandler(testContext.Context);

        var result = await handler.Handle(
            new GetBlueprintListQuery(0, 0, null, null, null, null, true, CurrentExpertId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value!.TotalCount);
        Assert.Equal(["active-a", "active-b", "own-hidden"], result.Value.Items.Select(item => item.BlueprintId));
        Assert.Equal(2, result.Value.Items[0].SectionCount);
        Assert.Equal(2, result.Value.Items[0].DetailSlotCount);
        Assert.DoesNotContain(result.Value.Items, item => item.BlueprintId == "other-hidden");
    }

    [Fact]
    public async Task Pending_ExcludesCurrentExpertsBlueprints()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        AddBlueprint(testContext, "mine", "Mine", CurrentExpertId, BlueprintStatuses.PendingReview);
        AddBlueprint(testContext, "peer", "Peer", OtherExpertId, BlueprintStatuses.PendingReview);
        AddBlueprint(testContext, "draft", "Draft", OtherExpertId, BlueprintStatuses.Draft);
        await testContext.Context.SaveChangesAsync();
        var handler = new GetPendingBlueprintsQueryHandler(testContext.Context);

        var result = await handler.Handle(
            new GetPendingBlueprintsQuery(1, 20, CurrentExpertId),
            CancellationToken.None);

        var item = Assert.Single(result.Value!.Items);
        Assert.Equal("peer", item.BlueprintId);
    }

    [Fact]
    public async Task Detail_ReturnsOrderedAggregateAndTaxonomyLabels()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        var blueprint = AddBlueprint(
            testContext,
            "detail-blueprint",
            "Detail",
            CurrentExpertId,
            BlueprintStatuses.Draft,
            sectionCount: 2,
            reverseSectionOrder: true);
        await testContext.Context.SaveChangesAsync();
        var handler = new GetBlueprintDetailQueryHandler(testContext.Context);

        var result = await handler.Handle(
            new GetBlueprintDetailQuery(blueprint.BlueprintId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal([1, 2], result.Value!.Sections.Select(section => section.SectionOrder));
        Assert.All(result.Value.Sections, section =>
        {
            var detail = Assert.Single(section.Details);
            Assert.Equal("Calculus", detail.TagName);
            Assert.Equal("Easy", detail.DifficultyName);
            Assert.Equal(1, detail.DifficultyLevel);
        });
    }

    [Fact]
    public async Task Detail_DeactivatedBlueprint_ReturnsNotFound()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferenceDataAsync(testContext);
        AddBlueprint(testContext, "hidden", "Hidden", CurrentExpertId, BlueprintStatuses.Deactivated);
        await testContext.Context.SaveChangesAsync();
        var handler = new GetBlueprintDetailQueryHandler(testContext.Context);

        var result = await handler.Handle(
            new GetBlueprintDetailQuery("hidden"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.NotFound, result.Error);
    }

    private static CreateBlueprintCommandHandler CreateHandler(TestGenInMemoryContext testContext)
        => new(testContext.Context, CreateValidator(testContext));

    private static BlueprintAggregateValidator CreateValidator(TestGenInMemoryContext testContext)
        => new(testContext.Context);

    private static BlueprintRequest ValidRequest()
        => new()
        {
            BlueprintName = "Blueprint THPT",
            Grade = 12,
            TotalQuestions = 10,
            DurationMinutes = 20,
            Sections =
            [
                new BlueprintSectionRequest
                {
                    SectionOrder = 1,
                    SectionCode = "I",
                    SectionName = "Trắc nghiệm",
                    QuestionType = BlueprintQuestionTypes.SingleChoice,
                    TotalQuestions = 10,
                    DefaultPointPerQuestion = 0.25m,
                    Details =
                    [
                        new BlueprintDetailRequest
                        {
                            TagId = Grade12TopicId,
                            DifficultyId = EasyDifficultyId,
                            Quantity = 10
                        }
                    ]
                }
            ]
        };

    private static async Task SeedReferenceDataAsync(TestGenInMemoryContext testContext)
    {
        testContext.Context.Experts.AddRange(
            new ExpertReadModel { ExpertId = CurrentExpertId },
            new ExpertReadModel { ExpertId = OtherExpertId });
        testContext.Context.TagTopics.AddRange(
            new TagTopicReadModel
            {
                TagId = Grade12TopicId,
                TagName = "Calculus",
                Grade = 12,
                IsActive = true,
                DisplayOrder = 1
            },
            new TagTopicReadModel
            {
                TagId = Grade11TopicId,
                TagName = "Algebra 11",
                Grade = 11,
                IsActive = true,
                DisplayOrder = 2
            },
            new TagTopicReadModel
            {
                TagId = InactiveTopicId,
                TagName = "Inactive topic",
                Grade = 12,
                IsActive = false,
                DisplayOrder = 3
            });
        testContext.Context.TagDifficulties.AddRange(
            new TagDifficultyReadModel
            {
                DifficultyId = EasyDifficultyId,
                DifficultyName = "Easy",
                LevelValue = 1,
                DisplayOrder = 1,
                IsActive = true
            },
            new TagDifficultyReadModel
            {
                DifficultyId = InactiveDifficultyId,
                DifficultyName = "Inactive difficulty",
                LevelValue = 2,
                DisplayOrder = 2,
                IsActive = false
            });

        await testContext.Context.SaveChangesAsync();
    }

    private static Blueprint AddBlueprint(
        TestGenInMemoryContext testContext,
        string id,
        string name,
        string expertId,
        string status,
        int sectionCount = 1,
        bool reverseSectionOrder = false)
    {
        var blueprint = new Blueprint
        {
            BlueprintId = id,
            BlueprintName = name,
            Grade = 12,
            TotalQuestions = sectionCount,
            DurationMinutes = 15,
            ExpertId = expertId,
            Status = status
        };

        for (var index = 1; index <= sectionCount; index++)
        {
            var order = reverseSectionOrder ? sectionCount - index + 1 : index;
            var sectionId = $"{id}-section-{index}";
            var section = new BlueprintSection
            {
                BlueprintSectionId = sectionId,
                BlueprintId = id,
                SectionOrder = order,
                SectionName = $"Section {order}",
                QuestionType = BlueprintQuestionTypes.SingleChoice,
                TotalQuestions = 1,
                DefaultPointPerQuestion = 1m
            };
            section.Details.Add(new BlueprintDetail
            {
                BlueprintDetailId = $"{sectionId}-detail",
                BlueprintId = id,
                BlueprintSectionId = sectionId,
                TagId = Grade12TopicId,
                DifficultyId = EasyDifficultyId,
                Quantity = 1
            });
            blueprint.Sections.Add(section);
        }

        testContext.Context.Blueprints.Add(blueprint);
        return blueprint;
    }
}
