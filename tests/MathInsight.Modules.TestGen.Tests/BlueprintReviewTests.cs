using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.ReviewBlueprint;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Persistence.Entities;
using MathInsight.Modules.TestGen.Persistence.ReadModels;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintReviewTests
{
    private const string OwnerId = "review-owner";
    private const string ReviewerId = "review-peer";

    [Fact]
    public async Task Approve_PendingPeerBlueprint_SetsApprovedAuditAndClearsNote()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.PendingReview);
        blueprint.ReviewNote = "Old rejection note";
        await testContext.Context.SaveChangesAsync();
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await CreateHandler(testContext).Handle(
            Command(blueprint.BlueprintId, BlueprintReviewActions.Approve, "ignored"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BlueprintStatuses.Approved, blueprint.Status);
        Assert.Equal(ReviewerId, blueprint.ApprovedBy);
        Assert.Null(blueprint.ReviewNote);
        Assert.NotNull(blueprint.ReviewTime);
        Assert.InRange(blueprint.ReviewTime.Value, before, DateTime.UtcNow);
        Assert.Equal(blueprint.ReviewTime, result.Value!.ReviewTime);
    }

    [Fact]
    public async Task Reject_PendingPeerBlueprint_StoresTrimmedNoteAndAudit()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.PendingReview);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            Command(blueprint.BlueprintId, "reject", "  Incorrect distribution  "),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(BlueprintStatuses.Rejected, blueprint.Status);
        Assert.Equal("Incorrect distribution", blueprint.ReviewNote);
        Assert.Equal(ReviewerId, blueprint.ApprovedBy);
        Assert.NotNull(blueprint.ReviewTime);
    }

    [Fact]
    public async Task Review_OwnPendingBlueprint_ReturnsSelfReviewForbidden()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.PendingReview);
        await testContext.Context.SaveChangesAsync();
        var handler = CreateHandler(testContext);

        var result = await handler.Handle(
            new ReviewBlueprintCommand(
                blueprint.BlueprintId,
                new ReviewBlueprintRequest { Action = BlueprintReviewActions.Approve },
                OwnerId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.SelfReviewForbidden, result.Error);
        Assert.Equal(BlueprintStatuses.PendingReview, blueprint.Status);
        Assert.Null(blueprint.ApprovedBy);
    }

    [Fact]
    public async Task Review_NonPendingBlueprint_ReturnsStatusInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.Draft);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            Command(blueprint.BlueprintId, BlueprintReviewActions.Approve),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.StatusInvalid, result.Error);
        Assert.Equal(BlueprintStatuses.Draft, blueprint.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Reject_MissingNote_ReturnsReviewNoteRequired(string? note)
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.PendingReview);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            Command(blueprint.BlueprintId, BlueprintReviewActions.Reject, note),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.ReviewNoteRequired, result.Error);
        Assert.Equal(BlueprintStatuses.PendingReview, blueprint.Status);
    }

    [Fact]
    public async Task Reject_OverlongNote_ReturnsReviewNoteTooLong()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.PendingReview);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            Command(blueprint.BlueprintId, BlueprintReviewActions.Reject, new string('x', 2001)),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.ReviewNoteTooLong, result.Error);
        Assert.Equal(BlueprintStatuses.PendingReview, blueprint.Status);
    }

    [Fact]
    public async Task Review_InvalidAction_ReturnsRequestInvalid()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.PendingReview);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            Command(blueprint.BlueprintId, "Publish"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(BlueprintErrors.RequestInvalid, result.Error);
        Assert.Equal(BlueprintStatuses.PendingReview, blueprint.Status);
    }

    [Fact]
    public async Task Review_UnknownReviewer_ReturnsInvalidToken()
    {
        await using var testContext = TestGenInMemoryContext.Create();
        await SeedExpertsAsync(testContext);
        var blueprint = AddBlueprint(testContext, BlueprintStatuses.PendingReview);
        await testContext.Context.SaveChangesAsync();

        var result = await CreateHandler(testContext).Handle(
            new ReviewBlueprintCommand(
                blueprint.BlueprintId,
                new ReviewBlueprintRequest { Action = BlueprintReviewActions.Approve },
                "missing-reviewer"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal("AUTH_INVALID_TOKEN", result.Error!.Code);
        Assert.Equal(BlueprintStatuses.PendingReview, blueprint.Status);
    }

    private static ReviewBlueprintCommandHandler CreateHandler(TestGenInMemoryContext testContext)
        => new(testContext.Context);

    private static ReviewBlueprintCommand Command(
        string blueprintId,
        string action,
        string? note = null)
        => new(
            blueprintId,
            new ReviewBlueprintRequest
            {
                Action = action,
                ReviewNote = note
            },
            ReviewerId);

    private static async Task SeedExpertsAsync(TestGenInMemoryContext testContext)
    {
        testContext.Context.Experts.AddRange(
            new ExpertReadModel { ExpertId = OwnerId },
            new ExpertReadModel { ExpertId = ReviewerId });
        await testContext.Context.SaveChangesAsync();
    }

    private static Blueprint AddBlueprint(TestGenInMemoryContext testContext, string status)
    {
        var blueprint = new Blueprint
        {
            BlueprintId = Guid.NewGuid().ToString(),
            BlueprintName = "Peer review blueprint",
            Grade = 12,
            TotalQuestions = 1,
            DurationMinutes = 10,
            ExpertId = OwnerId,
            Status = status
        };
        testContext.Context.Blueprints.Add(blueprint);
        return blueprint;
    }
}
