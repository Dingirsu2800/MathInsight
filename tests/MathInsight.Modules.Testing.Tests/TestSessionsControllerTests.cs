using System.Security.Claims;
using MathInsight.Modules.Testing.Commands.StartSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Controllers;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Queries.GetInProgressSession;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace MathInsight.Modules.Testing.Tests;

public sealed class TestSessionsControllerTests
{
    [Fact]
    public async Task StartSession_Duplicate_ReturnsExistingSessionId()
    {
        const string existingSessionId = "existing-session";
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(
                It.IsAny<StartSessionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartSessionResponse>.Failure(TestingErrors.SessionAlreadyInProgress));
        mediator
            .Setup(item => item.Send(
                It.IsAny<GetInProgressSessionQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<string>.Success(existingSessionId));
        var controller = CreateController(mediator.Object);

        var result = await controller.StartSession(
            new StartSessionRequest(TestDataSeeder.ActiveTestId),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var response = Assert.IsType<SessionAlreadyInProgressResponse>(conflict.Value);
        Assert.Equal("TESTING_SESSION_ALREADY_IN_PROGRESS", response.Code);
        Assert.Equal(existingSessionId, response.ExistingSessionId);
    }

    [Fact]
    public async Task StartSession_AccessDenied_Returns403WithStableCode()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(item => item.Send(
                It.IsAny<StartSessionCommand>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<StartSessionResponse>.Failure(TestingErrors.TestAccessDenied));

        var controller = CreateController(mediator.Object);

        var result = await controller.StartSession(
            new StartSessionRequest(TestDataSeeder.ActiveTestId),
            CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, forbidden.StatusCode);
        var error = Assert.IsType<ApiErrorResponse>(forbidden.Value);
        Assert.Equal("TESTING_TEST_ACCESS_DENIED", error.Code);
    }

    private static TestSessionsController CreateController(IMediator mediator)
        => new(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim("account_id", TestDataSeeder.StudentId)],
                        "Test"))
                }
            }
        };
}
