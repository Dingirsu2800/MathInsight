using System.Security.Claims;
using MathInsight.Modules.TestGen.Blueprints;
using MathInsight.Modules.TestGen.Commands.ArchiveSharedBlueprintExam;
using MathInsight.Modules.TestGen.Commands.GenerateSharedBlueprintExam;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Controllers;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Queries.GetExpertTestPreview;
using MathInsight.Modules.TestGen.Queries.ResolveSharedTestCode;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class SharedBlueprintExamControllerTests
{
    private const string ExpertId = "controller-expert";
    private const string StudentId = "controller-student";

    [Fact]
    public async Task GenerateSharedBlueprintExam_UsesExpertClaimAndReturns201()
    {
        var response = GenerationResponse();
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.Is<GenerateSharedBlueprintExamCommand>(command =>
                    command.BlueprintId == "controller-blueprint" &&
                    command.ExpertId == ExpertId &&
                    command.TestName == "Controller exam" &&
                    command.DurationMinutes == 50),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GenerateSharedBlueprintExamResponse>.Success(response));
        var controller = CreateBlueprintController(mediator.Object);

        var result = await controller.GenerateSharedBlueprintExam(
            "controller-blueprint",
            new GenerateSharedBlueprintExamRequest
            {
                TestName = "Controller exam",
                DurationMinutes = 50
            },
            CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        Assert.Same(response, created.Value);
        mediator.VerifyAll();
    }

    [Theory]
    [InlineData("not-found", 404)]
    [InlineData("forbidden", 403)]
    [InlineData("status", 422)]
    [InlineData("score", 422)]
    [InlineData("version", 422)]
    [InlineData("pool", 409)]
    [InlineData("conflict", 409)]
    public async Task GenerateSharedBlueprintExam_MapsKeyErrors(string errorKind, int expectedStatus)
    {
        var error = errorKind switch
        {
            "not-found" => BlueprintErrors.NotFound,
            "forbidden" => BlueprintErrors.MutationForbidden,
            "status" => BlueprintErrors.StatusInvalid,
            "score" => TestGenerationErrors.ScoreBudgetMismatch,
            "version" => TestGenerationErrors.QuestionVersionMissing,
            "pool" => TestGenerationErrors.QuestionPoolInsufficient,
            _ => TestGenerationErrors.GenerationConflict
        };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.IsAny<GenerateSharedBlueprintExamCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GenerateSharedBlueprintExamResponse>.Failure(error));
        var controller = CreateBlueprintController(mediator.Object);

        var result = await controller.GenerateSharedBlueprintExam(
            "controller-blueprint",
            new GenerateSharedBlueprintExamRequest { TestName = "Exam", DurationMinutes = 30 },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.Equal(error.Code, Assert.IsType<ApiErrorResponse>(objectResult.Value).Code);
    }

    [Fact]
    public async Task ExpertPreview_UsesExpertClaim()
    {
        var response = new ExpertTestPreviewResponse(
            "controller-test",
            "controller-blueprint",
            "Controller exam",
            "CTRL2345",
            "Active",
            30,
            1,
            1m,
            DateTime.UtcNow,
            []);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.Is<GetExpertTestPreviewQuery>(query =>
                    query.TestId == "controller-test" && query.ExpertId == ExpertId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExpertTestPreviewResponse>.Success(response));
        var controller = CreateGeneratedTestsController(mediator.Object, ExpertId, "Expert");

        var result = await controller.GetExpertPreview("controller-test", CancellationToken.None);

        Assert.Same(response, Assert.IsType<OkObjectResult>(result).Value);
        mediator.VerifyAll();
    }

    [Fact]
    public async Task Archive_UsesExpertClaim()
    {
        var response = new UpdateGeneratedTestStatusResponse("controller-test", "Archived");
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.Is<ArchiveSharedBlueprintExamCommand>(command =>
                    command.TestId == "controller-test" &&
                    command.ExpertId == ExpertId &&
                    command.Status == "Archived"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<UpdateGeneratedTestStatusResponse>.Success(response));
        var controller = CreateGeneratedTestsController(mediator.Object, ExpertId, "Expert");

        var result = await controller.UpdateStatus(
            "controller-test",
            new UpdateGeneratedTestStatusRequest { Status = "Archived" },
            CancellationToken.None);

        Assert.Same(response, Assert.IsType<OkObjectResult>(result).Value);
        mediator.VerifyAll();
    }

    [Theory]
    [InlineData("forbidden", 403)]
    [InlineData("missing", 404)]
    [InlineData("version", 422)]
    public async Task ExpertPreview_MapsKeyErrors(string errorKind, int expectedStatus)
    {
        var error = errorKind switch
        {
            "forbidden" => BlueprintErrors.MutationForbidden,
            "missing" => TestGenerationErrors.GeneratedTestNotFound,
            _ => TestGenerationErrors.QuestionVersionMissing
        };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.IsAny<GetExpertTestPreviewQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExpertTestPreviewResponse>.Failure(error));
        var controller = CreateGeneratedTestsController(mediator.Object, ExpertId, "Expert");

        var result = await controller.GetExpertPreview("controller-test", CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.Equal(error.Code, Assert.IsType<ApiErrorResponse>(objectResult.Value).Code);
    }

    [Fact]
    public async Task ResolveCode_UnavailableErrorMapsTo404AndUsesStudentClaim()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.Is<ResolveSharedTestCodeQuery>(query =>
                    query.StudentId == StudentId && query.TestCode == "CODE2345"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SharedBlueprintExamResponse>.Failure(
                TestGenerationErrors.TestCodeNotAvailable));
        var controller = CreateGeneratedTestsController(mediator.Object, StudentId, "Student");

        var result = await controller.ResolveTestCode(
            new ResolveTestCodeRequest { TestCode = "CODE2345" },
            CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        Assert.Equal(
            TestGenerationErrors.TestCodeNotAvailable.Code,
            Assert.IsType<ApiErrorResponse>(notFound.Value).Code);
        mediator.VerifyAll();
    }

    private static GenerateSharedBlueprintExamResponse GenerationResponse()
        => new(
            "controller-test",
            "controller-blueprint",
            "CTRL2345",
            "BlueprintExam",
            "Active",
            "System",
            null,
            "Controller exam",
            50,
            1,
            1m,
            "BlueprintBudget",
            DateTime.UtcNow,
            []);

    private static BlueprintsController CreateBlueprintController(IMediator mediator)
        => new(mediator)
        {
            ControllerContext = ControllerContextFor(ExpertId, "Expert")
        };

    private static GeneratedTestsController CreateGeneratedTestsController(
        IMediator mediator,
        string accountId,
        string role)
        => new(mediator)
        {
            ControllerContext = ControllerContextFor(accountId, role)
        };

    private static ControllerContext ControllerContextFor(string accountId, string role)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("account_id", accountId),
                new Claim(ClaimTypes.NameIdentifier, "fallback-account"),
                new Claim(ClaimTypes.Role, role)
            ],
            "TestAuth");
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
