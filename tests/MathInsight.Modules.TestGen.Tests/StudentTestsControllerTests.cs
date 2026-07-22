using System.Security.Claims;
using MathInsight.Modules.TestGen.Commands.GenerateBlueprintExam;
using MathInsight.Modules.TestGen.Contracts.Tests;
using MathInsight.Modules.TestGen.Controllers;
using MathInsight.Modules.TestGen.Errors;
using MathInsight.Modules.TestGen.Queries.GetBlueprintExamOptions;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.TestGen.Tests;

public sealed class StudentTestsControllerTests
{
    private const string StudentId = "student-current";

    [Fact]
    public async Task Options_UsesAuthenticatedStudentClaim()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.Is<GetBlueprintExamOptionsQuery>(query => query.StudentId == StudentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<BlueprintExamOptionResponse>>.Success(
                Array.Empty<BlueprintExamOptionResponse>()));
        var controller = CreateController(mediator.Object);

        var result = await controller.GetBlueprintOptions(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        mediator.VerifyAll();
    }

    [Fact]
    public async Task Generate_UsesClaimAndReturns201()
    {
        var response = new GenerateBlueprintExamResponse(
            "test-id",
            "blueprint-id",
            "BlueprintExam",
            "Exam",
            45,
            20,
            10m,
            "BlueprintBudget",
            DateTime.UtcNow);
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.Is<GenerateBlueprintExamCommand>(command =>
                    command.StudentId == StudentId &&
                    command.BlueprintId == "blueprint-id"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GenerateBlueprintExamResponse>.Success(response));
        var controller = CreateController(mediator.Object);

        var result = await controller.GenerateBlueprintExam(
            new GenerateBlueprintExamRequest { BlueprintId = "blueprint-id" },
            CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);
        Assert.Same(response, objectResult.Value);
        mediator.VerifyAll();
    }

    [Theory]
    [InlineData("not-found", 404)]
    [InlineData("student", 404)]
    [InlineData("unavailable", 422)]
    [InlineData("grade", 422)]
    [InlineData("pool", 409)]
    public async Task Generate_MapsStableErrors(string errorKind, int expectedStatus)
    {
        var error = errorKind switch
        {
            "not-found" => TestGenerationErrors.BlueprintNotFound,
            "student" => TestGenerationErrors.StudentNotFound,
            "unavailable" => TestGenerationErrors.BlueprintUnavailable,
            "grade" => TestGenerationErrors.GradeMismatch,
            _ => TestGenerationErrors.InsufficientQuestions
        };
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.IsAny<GenerateBlueprintExamCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<GenerateBlueprintExamResponse>.Failure(error));
        var controller = CreateController(mediator.Object);

        var result = await controller.GenerateBlueprintExam(
            new GenerateBlueprintExamRequest { BlueprintId = "blueprint-id" },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.Equal(error.Code, Assert.IsType<ApiErrorResponse>(objectResult.Value).Code);
    }

    [Fact]
    public async Task Generate_MissingBody_ReturnsGenerationRequestError()
    {
        var controller = CreateController(Mock.Of<IMediator>());

        var result = await controller.GenerateBlueprintExam(null, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(
            TestGenerationErrors.RequestInvalid.Code,
            Assert.IsType<ApiErrorResponse>(badRequest.Value).Code);
    }

    [Fact]
    public async Task Generate_MissingIdentity_Returns401()
    {
        var controller = new StudentTestsController(Mock.Of<IMediator>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GenerateBlueprintExam(
            new GenerateBlueprintExamRequest { BlueprintId = "blueprint-id" },
            CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private static StudentTestsController CreateController(IMediator mediator)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim("account_id", StudentId),
                new Claim(ClaimTypes.NameIdentifier, "fallback-id"),
                new Claim(ClaimTypes.Role, "Student")
            ],
            "Test");

        return new StudentTestsController(mediator)
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
}
