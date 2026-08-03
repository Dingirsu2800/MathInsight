using System.Security.Claims;
using MathInsight.Modules.Recommender.Controllers;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Unit;

public sealed class RecommenderControllerTests
{
    [Fact]
    public async Task GetRecommendedLectures_MissingAccountId_ReturnsStableUnauthorizedError()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateController(mediator, new ClaimsPrincipal(new ClaimsIdentity()));

        var result = await controller.GetRecommendedLectures(CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("AUTH_INVALID_TOKEN", error.Code);
        mediator.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetRecommendedLectures_QueryThrows_ReturnsStableServiceUnavailableError()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<GetRecommendedLecturesQuery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("database detail must not reach client"));

        var controller = CreateController(
            mediator,
            new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "student_01")],
                "test")));

        var result = await controller.GetRecommendedLectures(CancellationToken.None);

        var unavailable = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailable.StatusCode);
        var error = Assert.IsType<ApiErrorResponse>(unavailable.Value);
        Assert.Equal("LECTURE_RECOMMENDATION_UNAVAILABLE", error.Code);
        Assert.DoesNotContain("database detail", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static RecommenderController CreateController(Mock<IMediator> mediator, ClaimsPrincipal user)
    {
        return new RecommenderController(
            mediator.Object,
            Mock.Of<ILogger<RecommenderController>>())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };
    }
}
