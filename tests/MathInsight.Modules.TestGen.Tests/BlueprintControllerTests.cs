using System.Security.Claims;
using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.CloneBlueprint;
using MathInsight.Modules.TestGen.Commands.CreateBlueprint;
using MathInsight.Modules.TestGen.Commands.DeleteBlueprint;
using MathInsight.Modules.TestGen.Commands.SubmitBlueprintForReview;
using MathInsight.Modules.TestGen.Commands.UpdateBlueprint;
using MathInsight.Modules.TestGen.Commands.ReviewBlueprint;
using MathInsight.Modules.TestGen.Contracts.Blueprints;
using MathInsight.Modules.TestGen.Controllers;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class BlueprintControllerTests
{
    [Fact]
    public async Task Update_NonOwnerError_MapsTo403()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<UpdateBlueprintCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UpdateBlueprintResponse>.Failure(BlueprintErrors.MutationForbidden));
        var controller = CreateController(mediator.Object);

        var result = await controller.UpdateBlueprint(
            "blueprint-id",
            ValidRequest(),
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        Assert.Equal(BlueprintErrors.MutationForbidden.Code, Assert.IsType<ApiErrorResponse>(objectResult.Value).Code);
    }

    [Fact]
    public async Task Update_NotFoundError_MapsTo404()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<UpdateBlueprintCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UpdateBlueprintResponse>.Failure(BlueprintErrors.NotFound));
        var controller = CreateController(mediator.Object);

        var result = await controller.UpdateBlueprint(
            "missing",
            ValidRequest(),
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(BlueprintErrors.NotFound.Code, Assert.IsType<ApiErrorResponse>(notFound.Value).Code);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("totals")]
    public async Task Submit_WorkflowErrors_MapTo422(string errorKind)
    {
        var error = errorKind == "status"
            ? BlueprintErrors.StatusInvalid
            : BlueprintErrors.TotalMismatch;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<SubmitBlueprintForReviewCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SubmitBlueprintResponse>.Failure(error));
        var controller = CreateController(mediator.Object);

        var result = await controller.SubmitBlueprintForReview(
            "blueprint-id",
            CancellationToken.None);

        var objectResult = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, objectResult.StatusCode);
        Assert.Equal(error.Code, Assert.IsType<ApiErrorResponse>(objectResult.Value).Code);
    }

    [Fact]
    public async Task Review_SelfReviewError_MapsTo403()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<ReviewBlueprintCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReviewBlueprintResponse>.Failure(BlueprintErrors.SelfReviewForbidden));
        var controller = CreateController(mediator.Object);

        var result = await controller.ReviewBlueprint(
            "blueprint-id",
            new ReviewBlueprintRequest { Action = BlueprintReviewActions.Approve },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Theory]
    [InlineData("required")]
    [InlineData("too-long")]
    public async Task Review_NoteErrors_MapTo400(string errorKind)
    {
        var error = errorKind == "required"
            ? BlueprintErrors.ReviewNoteRequired
            : BlueprintErrors.ReviewNoteTooLong;
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<ReviewBlueprintCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ReviewBlueprintResponse>.Failure(error));
        var controller = CreateController(mediator.Object);

        var result = await controller.ReviewBlueprint(
            "blueprint-id",
            new ReviewBlueprintRequest { Action = BlueprintReviewActions.Reject },
            CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(error.Code, Assert.IsType<ApiErrorResponse>(badRequest.Value).Code);
    }

    [Fact]
    public async Task Clone_Success_Returns201()
    {
        var response = new CloneBlueprintResponse(
            "cloned-blueprint",
            "Blueprint (Copy)",
            BlueprintStatuses.Draft);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<CloneBlueprintCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CloneBlueprintResponse>.Success(response));
        var controller = CreateController(mediator.Object);

        var result = await controller.CloneBlueprint("source-blueprint", CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        Assert.Same(response, objectResult.Value);
    }

    [Fact]
    public async Task Delete_InUseError_MapsTo409()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<DeleteBlueprintCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DeleteBlueprintResponse>.Failure(BlueprintErrors.InUse));
        var controller = CreateController(mediator.Object);

        var result = await controller.DeleteBlueprint("blueprint-id", CancellationToken.None);

        var objectResult = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        Assert.Equal(BlueprintErrors.InUse.Code, Assert.IsType<ApiErrorResponse>(objectResult.Value).Code);
    }

    [Fact]
    public async Task Create_TaxonomyError_MapsTo400AndUsesAccountClaim()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(
                It.Is<CreateBlueprintCommand>(command =>
                    command.ExpertId == "controller-expert"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateBlueprintResponse>.Failure(BlueprintErrors.TaxonomyInvalid));
        var controller = CreateController(mediator.Object);

        var result = await controller.CreateBlueprint(ValidRequest(), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            BlueprintErrors.TaxonomyInvalid.Code,
            Assert.IsType<ApiErrorResponse>(badRequest.Value).Code);
        mediator.VerifyAll();
    }

    [Fact]
    public async Task Create_StructureError_MapsTo422()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<CreateBlueprintCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CreateBlueprintResponse>.Failure(BlueprintErrors.StructureInvalid));
        var controller = CreateController(mediator.Object);

        var result = await controller.CreateBlueprint(ValidRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<UnprocessableEntityObjectResult>(result);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, objectResult.StatusCode);
    }

    private static BlueprintsController CreateController(IMediator mediator)
    {
        var identity = new ClaimsIdentity(
            [new Claim("account_id", "controller-expert")],
            "TestAuth");
        return new BlueprintsController(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private static BlueprintRequest ValidRequest()
        => new()
        {
            BlueprintName = "Controller blueprint",
            Grade = 12,
            TotalQuestions = 1,
            DurationMinutes = 10,
            Sections =
            [
                new BlueprintSectionRequest
                {
                    SectionOrder = 1,
                    SectionName = "Section I",
                    QuestionType = BlueprintQuestionTypes.SingleChoice,
                    TotalQuestions = 1,
                    DefaultPointPerQuestion = 1m,
                    Details =
                    [
                        new BlueprintDetailRequest
                        {
                            TagId = "topic",
                            DifficultyId = "difficulty",
                            Quantity = 1
                        }
                    ]
                }
            ]
        };
}
