using System.Security.Claims;
using MathInsight.Modules.Recommender.Controllers;
using MathInsight.Modules.Recommender.Contracts;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using MathInsight.Modules.Recommender.Queries.GetRecommendedMaterials;
using MathInsight.Modules.Recommender.Queries.GetWeakTags;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests;

/// <summary>
/// TC-INT-RecommenderController-001..007
/// Integration tests for RecommenderController (UC-52, UC-53, UC-54).
/// Tests HTTP status code mapping for GetWeakTags, GetRecommendedLectures, GetRecommendedMaterials.
/// Uses Mock&lt;IMediator&gt; — no WebApplicationFactory required.
/// </summary>
public sealed class RecommenderControllerTests
{
    private const string StudentId = "student-rcm-ctrl-001";

    // ── Helper ───────────────────────────────────────────────────────────────

    private static RecommenderController CreateController(
        IMediator mediator,
        bool withStudentClaim = true)
    {
        var claims = withStudentClaim
            ? new[] { new Claim(ClaimTypes.NameIdentifier, StudentId) }
            : Array.Empty<Claim>();

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

    // ── TC-INT-RecommenderController-001 ─────────────────────────────────────

    /// <summary>
    /// TC-INT-RecommenderController-001: UC-52 — Student has weak tags.
    /// Handler returns non-empty list → 200 OK.
    /// </summary>
    [Fact]
    public async Task GetWeakTags_StudentHasWeakTags_Returns200WithList()
    {
        IReadOnlyList<WeakTagDto> weakTags =
        [
            new WeakTagDto("TAG-001", "Đạo hàm",   2.5m, NumberDone: 5),
            new WeakTagDto("TAG-002", "Tích phân",  3.8m, NumberDone: 3)
        ];

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.Is<GetWeakTagsQuery>(q => q.StudentId == StudentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(weakTags);

        var controller = CreateController(mediator.Object);

        var result = await controller.GetWeakTags(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<WeakTagDto>>(ok.Value);
        Assert.Equal(2, list.Count);
        Assert.All(list, t => Assert.True(t.OfficialPoint < 5.00m));
    }

    // ── TC-INT-RecommenderController-002 ─────────────────────────────────────

    /// <summary>
    /// TC-INT-RecommenderController-002: UC-52 — Student has no weak tags.
    /// Handler returns empty list → 200 OK with empty array (not 404).
    /// </summary>
    [Fact]
    public async Task GetWeakTags_NoWeakTags_Returns200WithEmptyList()
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.IsAny<GetWeakTagsQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IReadOnlyList<WeakTagDto>)[]);

        var controller = CreateController(mediator.Object);

        var result = await controller.GetWeakTags(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<WeakTagDto>>(ok.Value);
        Assert.Empty(list);
    }

    // ── TC-INT-RecommenderController-003 ─────────────────────────────────────

    /// <summary>
    /// TC-INT-RecommenderController-003: UC-52 — Missing student identity.
    /// No NameIdentifier claim → 401 Unauthorized; handler never called.
    /// </summary>
    [Fact]
    public async Task GetWeakTags_MissingStudentClaim_Returns401()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateController(mediator.Object, withStudentClaim: false);

        var result = await controller.GetWeakTags(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<GetWeakTagsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-INT-RecommenderController-004 ─────────────────────────────────────

    /// <summary>
    /// TC-INT-RecommenderController-004: UC-53 — Student has weak tags → recommended lectures returned.
    /// Handler returns list → 200 OK.
    /// </summary>
    [Fact]
    public async Task GetRecommendedLectures_StudentWithWeakTags_Returns200WithLectures()
    {
        IReadOnlyList<RecommendedLectureResponse> lectures =
        [
            new RecommendedLectureResponse(
                LectureId:       "LEC-001",
                Title:           "Bài 1: Đạo hàm cơ bản",
                Description:     null,
                TagId:           "TAG-001",
                TagName:         "Đạo hàm",
                OfficialPoint:   2.5m,
                IsRemedial:      true,
                DifficultyLevel: 1)
        ];

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.Is<GetRecommendedLecturesQuery>(q => q.StudentId == StudentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(lectures);

        var controller = CreateController(mediator.Object);

        var result = await controller.GetRecommendedLectures(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<RecommendedLectureResponse>>(ok.Value);
        Assert.Single(list);
    }

    // ── TC-INT-RecommenderController-005 ─────────────────────────────────────

    /// <summary>
    /// TC-INT-RecommenderController-005: UC-53 — Missing student identity.
    /// No NameIdentifier claim → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetRecommendedLectures_MissingStudentClaim_Returns401()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateController(mediator.Object, withStudentClaim: false);

        var result = await controller.GetRecommendedLectures(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<GetRecommendedLecturesQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-INT-RecommenderController-006 ─────────────────────────────────────

    /// <summary>
    /// TC-INT-RecommenderController-006: UC-54 — Student has weak tags → recommended materials returned.
    /// Handler returns list → 200 OK.
    /// </summary>
    [Fact]
    public async Task GetRecommendedMaterials_StudentWithWeakTags_Returns200WithMaterials()
    {
        IReadOnlyList<RecommendedMaterialResponse> materials =
        [
            new RecommendedMaterialResponse(
                MaterialId:    "MAT-001",
                Title:         "Tài liệu đạo hàm",
                Description:   null,
                FileUrl:       null,
                MaterialType:  null,
                TagId:         "TAG-001",
                TagName:       "Đạo hàm",
                OfficialPoint: 2.5m,
                IsRemedial:    true)
        ];

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(
                It.Is<GetRecommendedMaterialsQuery>(q => q.StudentId == StudentId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(materials);

        var controller = CreateController(mediator.Object);

        var result = await controller.GetRecommendedMaterials(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var list = Assert.IsAssignableFrom<IReadOnlyList<RecommendedMaterialResponse>>(ok.Value);
        Assert.Single(list);
    }

    // ── TC-INT-RecommenderController-007 ─────────────────────────────────────

    /// <summary>
    /// TC-INT-RecommenderController-007: UC-54 — Missing student identity.
    /// No NameIdentifier claim → 401 Unauthorized.
    /// </summary>
    [Fact]
    public async Task GetRecommendedMaterials_MissingStudentClaim_Returns401()
    {
        var mediator = new Mock<IMediator>();
        var controller = CreateController(mediator.Object, withStudentClaim: false);

        var result = await controller.GetRecommendedMaterials(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<GetRecommendedMaterialsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
