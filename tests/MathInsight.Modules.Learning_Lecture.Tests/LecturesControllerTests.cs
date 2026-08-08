using System.Security.Claims;
using MathInsight.Modules.Learning_Lecture.Contracts;
using MathInsight.Modules.Learning_Lecture.Controllers;
using MathInsight.Modules.Learning_Lecture.Errors;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MathInsight.Modules.Learning_Lecture.Tests;

public sealed class LecturesControllerTests
{
    [Fact]
    public async Task CreateLecture_InactiveTopic_ReturnsConflict()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<Commands.Lectures.CreateLectureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LectureDto>.Failure(LearningErrors.LectureTopicInactive));

        var result = await CreateController(mediator).CreateLecture(
            new CreateLectureRequest("Lecture", "Content", null, null, "topic-1", "difficulty-1", null, null),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal("LECTURE_TOPIC_INACTIVE", Assert.IsType<ApiErrorResponse>(conflict.Value).Code);
    }

    [Fact]
    public async Task CreateLecture_InactiveDifficulty_ReturnsConflict()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(x => x.Send(It.IsAny<Commands.Lectures.CreateLectureCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<LectureDto>.Failure(LearningErrors.LectureDifficultyInactive));

        var result = await CreateController(mediator).CreateLecture(
            new CreateLectureRequest("Lecture", "Content", null, null, "topic-1", "difficulty-1", null, null),
            CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
        Assert.Equal("LECTURE_DIFFICULTY_INACTIVE", Assert.IsType<ApiErrorResponse>(conflict.Value).Code);
    }

    private static LecturesController CreateController(Mock<IMediator> mediator)
    {
        return new LecturesController(mediator.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.NameIdentifier, "teacher-1")], "test"))
                }
            }
        };
    }
}
