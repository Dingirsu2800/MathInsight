using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.SubmitBlueprintForReview;
using MathInsight.Modules.TestGen.Commands.UpdateBlueprint;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Modules.TestGen.Validation;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintUpdateAndSubmitTests
{
    private const string OwnerId = "update-owner";
    private const string OtherExpertId = "update-other";
    private const string ActiveTopicId = "update-topic-active";
    private const string InactiveTopicId = "update-topic-inactive";
    private const string DifficultyId = "update-difficulty";

    [Fact]
    public async Task Update_OwnedRejectedBlueprint_ReplacesChildrenAndKeepsReviewAudit()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Rejected);
        blueprint.ApprovedBy = OtherExpertId;
        blueprint.ReviewNote = "Needs changes";
        blueprint.ReviewTime = new DateTime(2026, 7, 1, 1, 2, 3, DateTimeKind.Utc);
        await testContext.Context.SaveChangesAsync();
        var oldSectionId = blueprint.Sections.Single().BlueprintSectionId;
        var request = ValidRequest();
        request.BlueprintName = "  Updated blueprint  ";
        var handler = CreateUpdateHandler(testContext);

        var result = await handler.Handle(
            new UpdateBlueprintCommand(blueprint.BlueprintId, request, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var updated = await testContext.Context.Blueprints
            .Include(item => item.Sections)
                .ThenInclude(section => section.Details)
            .SingleAsync(item => item.BlueprintId == blueprint.BlueprintId);
        Assert.Equal("Updated blueprint", updated.BlueprintName);
        Assert.Equal(BlueprintStatuses.Rejected, updated.Status);
        Assert.Equal(OtherExpertId, updated.ApprovedBy);
        Assert.Equal("Needs changes", updated.ReviewNote);
        Assert.NotNull(updated.ReviewTime);
        Assert.DoesNotContain(updated.Sections, section => section.BlueprintSectionId == oldSectionId);
        Assert.All(updated.Sections, section => Assert.Equal(36, section.BlueprintSectionId.Length));
        Assert.All(
            updated.Sections.SelectMany(section => section.Details),
            detail => Assert.Equal(updated.BlueprintId, detail.BlueprintId));
        Assert.False(await testContext.Context.BlueprintSections.AnyAsync(section => section.BlueprintSectionId == oldSectionId));
    }

    [Fact]
    public async Task Update_NonOwner_ReturnsForbiddenWithoutMutation()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft);
        await testContext.Context.SaveChangesAsync();
        var handler = CreateUpdateHandler(testContext);

        var result = await handler.Handle(
            new UpdateBlueprintCommand(blueprint.BlueprintId, ValidRequest(), OtherExpertId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.MutationForbidden, result.Error);
        Assert.Equal("Original blueprint", blueprint.BlueprintName);
        Assert.Single(blueprint.Sections);
    }

    [Fact]
    public async Task Update_ApprovedBlueprint_ReturnsStatusInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Approved);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateUpdateHandler(testContext).Handle(
            new UpdateBlueprintCommand(blueprint.BlueprintId, ValidRequest(), OwnerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.StatusInvalid, result.Error);
    }

    [Fact]
    public async Task Update_InvalidReplacement_LeavesExistingChildrenUntouched()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft);
        await testContext.Context.SaveChangesAsync();
        var oldSectionId = blueprint.Sections.Single().BlueprintSectionId;
        var request = ValidRequest();
        request.Sections[0].Details[0].TagId = InactiveTopicId;

        var result = await CreateUpdateHandler(testContext).Handle(
            new UpdateBlueprintCommand(blueprint.BlueprintId, request, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.TaxonomyInvalid, result.Error);
        Assert.True(await testContext.Context.BlueprintSections.AnyAsync(section => section.BlueprintSectionId == oldSectionId));
        Assert.Equal("Original blueprint", blueprint.BlueprintName);
    }

    [Fact]
    public async Task Submit_ValidRejectedBlueprint_SetsPendingReviewAndClearsAudit()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Rejected);
        blueprint.ApprovedBy = OtherExpertId;
        blueprint.ReviewNote = "Fix this";
        blueprint.ReviewTime = DateTime.UtcNow;
        await testContext.Context.SaveChangesAsync();

        var result = await CreateSubmitHandler(testContext).Handle(
            new SubmitBlueprintForReviewCommand(blueprint.BlueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BlueprintStatuses.PendingReview, result.Value!.Status);
        Assert.Equal(BlueprintStatuses.PendingReview, blueprint.Status);
        Assert.Null(blueprint.ApprovedBy);
        Assert.Null(blueprint.ReviewNote);
        Assert.Null(blueprint.ReviewTime);
    }

    [Fact]
    public async Task Submit_SectionTotalMismatch_ReturnsTotalMismatch()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft);
        blueprint.TotalQuestions = 3;
        await testContext.Context.SaveChangesAsync();

        var result = await CreateSubmitHandler(testContext).Handle(
            new SubmitBlueprintForReviewCommand(blueprint.BlueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.TotalMismatch, result.Error);
        Assert.Equal(BlueprintStatuses.Draft, blueprint.Status);
    }

    [Fact]
    public async Task Submit_DetailTotalMismatch_ReturnsTotalMismatch()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft);
        blueprint.Sections.Single().Details.Single().Quantity = 1;
        await testContext.Context.SaveChangesAsync();

        var result = await CreateSubmitHandler(testContext).Handle(
            new SubmitBlueprintForReviewCommand(blueprint.BlueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.TotalMismatch, result.Error);
        Assert.Equal(BlueprintStatuses.Draft, blueprint.Status);
    }

    [Fact]
    public async Task Submit_InactivePersistedTaxonomy_ReturnsTaxonomyInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft, InactiveTopicId);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateSubmitHandler(testContext).Handle(
            new SubmitBlueprintForReviewCommand(blueprint.BlueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.TaxonomyInvalid, result.Error);
        Assert.Equal(BlueprintStatuses.Draft, blueprint.Status);
    }

    [Fact]
    public async Task Submit_NonOwner_ReturnsForbidden()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedReferencesAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateSubmitHandler(testContext).Handle(
            new SubmitBlueprintForReviewCommand(blueprint.BlueprintId, OtherExpertId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.MutationForbidden, result.Error);
        Assert.Equal(BlueprintStatuses.Draft, blueprint.Status);
    }

    private static UpdateBlueprintCommandHandler CreateUpdateHandler(TestGenInMemoryContext testContext)
        => new(testContext.Context, new BlueprintAggregateValidator(testContext.Context));

    private static SubmitBlueprintForReviewCommandHandler CreateSubmitHandler(TestGenInMemoryContext testContext)
        => new(testContext.Context, new BlueprintAggregateValidator(testContext.Context));

    private static BlueprintRequest ValidRequest()
        => new()
        {
            BlueprintName = "Updated blueprint",
            Grade = 12,
            TotalQuestions = 2,
            TotalScore = 0.5m,
            DurationMinutes = 30,
            Sections =
            [
                new BlueprintSectionRequest
                {
                    SectionOrder = 1,
                    SectionName = "Section I",
                    QuestionType = BlueprintQuestionTypes.SingleChoice,
                    TotalQuestions = 2,
                    ScoreBudget = 0.5m,
                    Details =
                    [
                        new BlueprintDetailRequest
                        {
                            TagId = ActiveTopicId,
                            DifficultyId = DifficultyId,
                            Quantity = 2
                        }
                    ]
                }
            ]
        };

    private static async Task SeedReferencesAsync(TestGenInMemoryContext testContext)
    {
        testContext.Context.Experts.AddRange(
            new ExpertReadModel { ExpertId = OwnerId },
            new ExpertReadModel { ExpertId = OtherExpertId });
        testContext.Context.TagTopics.AddRange(
            new TagTopicReadModel
            {
                TagId = ActiveTopicId,
                TagName = "Active topic",
                Grade = 12,
                IsActive = true,
                DisplayOrder = 1
            },
            new TagTopicReadModel
            {
                TagId = InactiveTopicId,
                TagName = "Inactive topic",
                Grade = 12,
                IsActive = false,
                DisplayOrder = 2
            });
        testContext.Context.TagDifficulties.Add(new TagDifficultyReadModel
        {
            DifficultyId = DifficultyId,
            DifficultyName = "Easy",
            LevelValue = 1,
            DisplayOrder = 1,
            IsActive = true
        });
        await testContext.Context.SaveChangesAsync();
    }

    private static Blueprint AddBlueprint(
        TestGenInMemoryContext testContext,
        string status,
        string topicId = ActiveTopicId)
    {
        var blueprintId = Guid.NewGuid().ToString();
        var sectionId = Guid.NewGuid().ToString();
        var blueprint = new Blueprint
        {
            BlueprintId = blueprintId,
            BlueprintName = "Original blueprint",
            Grade = 12,
            TotalQuestions = 2,
            TotalScore = 0.5m,
            DurationMinutes = 30,
            ExpertId = OwnerId,
            Status = status
        };
        var section = new BlueprintSection
        {
            BlueprintSectionId = sectionId,
            BlueprintId = blueprintId,
            SectionOrder = 1,
            SectionName = "Section I",
            QuestionType = BlueprintQuestionTypes.SingleChoice,
            TotalQuestions = 2,
            ScoreBudget = 0.5m
        };
        section.Details.Add(new BlueprintDetail
        {
            BlueprintDetailId = Guid.NewGuid().ToString(),
            BlueprintId = blueprintId,
            BlueprintSectionId = sectionId,
            TagId = topicId,
            DifficultyId = DifficultyId,
            Quantity = 2
        });
        blueprint.Sections.Add(section);
        testContext.Context.Blueprints.Add(blueprint);
        return blueprint;
    }
}
