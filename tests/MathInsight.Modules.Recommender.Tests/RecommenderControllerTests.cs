using System.Security.Claims;
using MathInsight.Modules.Recommender.Controllers;
using MathInsight.Modules.Recommender.Queries.GetWeakTags;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests;

public sealed class RecommenderControllerTests
{
    [Fact]
    public async Task WeakTags_AcceptsSemanticStudentIdClaim()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(instance => instance.Send(
                It.Is<GetWeakTagsQuery>(query => query.StudentId == "student_01"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var controller = CreateController(mediator.Object, "student_01");

        var result = await controller.GetWeakTags(default);

        Assert.IsType<OkObjectResult>(result);
        mediator.VerifyAll();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task WeakTags_MissingOrEmptyClaim_ReturnsStableUnauthorizedContract(string? studentId)
    {
        var controller = CreateController(Mock.Of<IMediator>(), studentId);

        var result = await controller.GetWeakTags(default);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        var error = Assert.IsType<ApiErrorResponse>(unauthorized.Value);
        Assert.Equal("AUTH_INVALID_TOKEN", error.Code);
        Assert.Equal("Invalid or missing account id.", error.Message);
    }

    private static RecommenderController CreateController(IMediator mediator, string? studentId)
    {
        var claims = studentId is null
            ? []
            : new[] { new Claim(ClaimTypes.NameIdentifier, studentId) };
        return new RecommenderController(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            }
        };
    }
}
