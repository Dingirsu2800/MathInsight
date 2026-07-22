using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.CloneBlueprint;
using MathInsight.Modules.TestGen.Commands.DeleteBlueprint;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;
using MathInsight.Shared.Results;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintCloneAndDeleteTests
{
    private const string OwnerId = "lifecycle-owner";
    private const string ClonerId = "lifecycle-cloner";

    [Fact]
    public async Task Clone_VisibleNonOwnedBlueprint_CreatesIndependentDraftAggregate()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var source = AddBlueprint(testContext, BlueprintStatuses.Approved);
        source.ApprovedBy = ClonerId;
        source.ReviewNote = "Approved note";
        source.ReviewTime = DateTime.UtcNow;
        await testContext.Context.SaveChangesAsync();
        var sourceSectionId = source.Sections.Single().BlueprintSectionId;
        var sourceDetailId = source.Sections.Single().Details.Single().BlueprintDetailId;

        var result = await new CloneBlueprintCommandHandler(testContext.Context).Handle(
            new CloneBlueprintCommand(source.BlueprintId, ClonerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var clone = await testContext.Context.Blueprints
            .Include(blueprint => blueprint.Sections)
                .ThenInclude(section => section.Details)
            .SingleAsync(blueprint => blueprint.BlueprintId == result.Value!.BlueprintId);
        Assert.NotEqual(source.BlueprintId, clone.BlueprintId);
        Assert.Equal("Original blueprint (Copy)", clone.BlueprintName);
        Assert.Equal(ClonerId, clone.ExpertId);
        Assert.Equal(BlueprintStatuses.Draft, clone.Status);
        Assert.Null(clone.ApprovedBy);
        Assert.Null(clone.ReviewNote);
        Assert.Null(clone.ReviewTime);
        Assert.Equal(source.Grade, clone.Grade);
        Assert.Equal(source.TotalQuestions, clone.TotalQuestions);
        Assert.Equal(source.DurationMinutes, clone.DurationMinutes);

        var clonedSection = Assert.Single(clone.Sections);
        var clonedDetail = Assert.Single(clonedSection.Details);
        Assert.NotEqual(sourceSectionId, clonedSection.BlueprintSectionId);
        Assert.NotEqual(sourceDetailId, clonedDetail.BlueprintDetailId);
        Assert.Equal(clone.BlueprintId, clonedSection.BlueprintId);
        Assert.Equal(clone.BlueprintId, clonedDetail.BlueprintId);
        Assert.Equal(clonedSection.BlueprintSectionId, clonedDetail.BlueprintSectionId);
        Assert.Equal(BlueprintStatuses.Approved, source.Status);
    }

    [Fact]
    public async Task Clone_NameAtMaximumLength_TruncatesBaseBeforeCopySuffix()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var source = AddBlueprint(testContext, BlueprintStatuses.Draft);
        source.BlueprintName = new string('A', 100);
        await testContext.Context.SaveChangesAsync();

        var result = await new CloneBlueprintCommandHandler(testContext.Context).Handle(
            new CloneBlueprintCommand(source.BlueprintId, ClonerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(100, result.Value!.BlueprintName.Length);
        Assert.EndsWith(" (Copy)", result.Value.BlueprintName, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clone_DeactivatedBlueprint_ReturnsNotFound()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var source = AddBlueprint(testContext, BlueprintStatuses.Deactivated);
        await testContext.Context.SaveChangesAsync();

        var result = await new CloneBlueprintCommandHandler(testContext.Context).Handle(
            new CloneBlueprintCommand(source.BlueprintId, ClonerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.NotFound, result.Error);
        Assert.Single(await testContext.Context.Blueprints.ToListAsync());
    }

    [Fact]
    public async Task Clone_UnknownExpert_ReturnsAuthenticationError()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var source = AddBlueprint(testContext, BlueprintStatuses.Draft);
        await testContext.Context.SaveChangesAsync();

        var result = await new CloneBlueprintCommandHandler(testContext.Context).Handle(
            new CloneBlueprintCommand(source.BlueprintId, "missing-expert"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ApplicationErrors.AuthInvalidToken, result.Error);
        Assert.Single(await testContext.Context.Blueprints.ToListAsync());
    }

    [Theory]
    [InlineData(BlueprintStatuses.Draft)]
    [InlineData(BlueprintStatuses.Rejected)]
    [InlineData(BlueprintStatuses.Approved)]
    public async Task Delete_UnusedEditableOrReviewedBlueprint_HardDeletesAggregate(string status)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, status);
        var blueprintId = blueprint.BlueprintId;
        var sectionId = blueprint.Sections.Single().BlueprintSectionId;
        var detailId = blueprint.Sections.Single().Details.Single().BlueprintDetailId;
        await testContext.Context.SaveChangesAsync();

        var result = await new DeleteBlueprintCommandHandler(testContext.Context).Handle(
            new DeleteBlueprintCommand(blueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.WasDeactivated);
        Assert.Null(result.Value.Status);
        Assert.False(await testContext.Context.Blueprints.AnyAsync(item => item.BlueprintId == blueprintId));
        Assert.False(await testContext.Context.BlueprintSections.AnyAsync(item => item.BlueprintSectionId == sectionId));
        Assert.False(await testContext.Context.BlueprintDetails.AnyAsync(item => item.BlueprintDetailId == detailId));
    }

    [Fact]
    public async Task Delete_PendingReviewBlueprint_ReturnsInUseWithoutMutation()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.PendingReview);
        await testContext.Context.SaveChangesAsync();

        var result = await new DeleteBlueprintCommandHandler(testContext.Context).Handle(
            new DeleteBlueprintCommand(blueprint.BlueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.InUse, result.Error);
        Assert.Equal(BlueprintStatuses.PendingReview, blueprint.Status);
    }

    [Fact]
    public async Task Delete_ActiveBlueprint_DeactivatesAndRetainsAggregate()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Active);
        await testContext.Context.SaveChangesAsync();

        var result = await new DeleteBlueprintCommandHandler(testContext.Context).Handle(
            new DeleteBlueprintCommand(blueprint.BlueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WasDeactivated);
        Assert.Equal(BlueprintStatuses.Deactivated, result.Value.Status);
        Assert.Equal(BlueprintStatuses.Deactivated, blueprint.Status);
        Assert.Single(blueprint.Sections);
    }

    [Fact]
    public async Task Delete_TestLinkedDraftBlueprint_DeactivatesAndRetainsHistory()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft);
        var test = new Test
        {
            TestId = Guid.NewGuid().ToString(),
            BlueprintId = blueprint.BlueprintId,
            TestStatus = "Active",
            TestMode = "BlueprintExam",
            GeneratedBy = "Expert",
            TestName = "Historical test",
            DurationMinutes = 30,
            TotalQuestions = 2
        };
        testContext.Context.Tests.Add(test);
        await testContext.Context.SaveChangesAsync();

        var result = await new DeleteBlueprintCommandHandler(testContext.Context).Handle(
            new DeleteBlueprintCommand(blueprint.BlueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.WasDeactivated);
        Assert.Equal(BlueprintStatuses.Deactivated, blueprint.Status);
        Assert.True(await testContext.Context.Tests.AnyAsync(item => item.TestId == test.TestId));
    }

    [Fact]
    public async Task Delete_NonOwner_ReturnsForbiddenWithoutMutation()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft);
        await testContext.Context.SaveChangesAsync();

        var result = await new DeleteBlueprintCommandHandler(testContext.Context).Handle(
            new DeleteBlueprintCommand(blueprint.BlueprintId, ClonerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.MutationForbidden, result.Error);
        Assert.True(await testContext.Context.Blueprints.AnyAsync(item => item.BlueprintId == blueprint.BlueprintId));
    }

    [Fact]
    public async Task Delete_DeactivatedBlueprint_ReturnsNotFound()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Deactivated);
        await testContext.Context.SaveChangesAsync();

        var result = await new DeleteBlueprintCommandHandler(testContext.Context).Handle(
            new DeleteBlueprintCommand(blueprint.BlueprintId, OwnerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.NotFound, result.Error);
    }

    private static async Task SeedExpertsAsync(TestGenInMemoryContext testContext)
    {
        testContext.Context.Experts.AddRange(
            new ExpertReadModel { ExpertId = OwnerId },
            new ExpertReadModel { ExpertId = ClonerId });
        await testContext.Context.SaveChangesAsync();
    }

    private static Blueprint AddBlueprint(TestGenInMemoryContext testContext, string status)
    {
        var blueprintId = Guid.NewGuid().ToString();
        var sectionId = Guid.NewGuid().ToString();
        var blueprint = new Blueprint
        {
            BlueprintId = blueprintId,
            BlueprintName = "Original blueprint",
            Grade = 12,
            TotalQuestions = 2,
            DurationMinutes = 30,
            ExpertId = OwnerId,
            Status = status
        };
        var section = new BlueprintSection
        {
            BlueprintSectionId = sectionId,
            BlueprintId = blueprintId,
            SectionOrder = 1,
            SectionCode = "I",
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
            TagId = "lifecycle-topic",
            DifficultyId = "lifecycle-difficulty",
            Quantity = 2
        });
        blueprint.Sections.Add(section);
        testContext.Context.Blueprints.Add(blueprint);
        return blueprint;
    }
}
