using System.Diagnostics;
using System.Security.Claims;
using MathInsight.Modules.Recommender.Controllers;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Queries.GetWeakTags;
using MathInsight.Modules.Recommender.Queries.GetRecommendedLectures;
using MathInsight.Modules.Recommender.Handlers;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Events;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Integration;

/// <summary>
/// TC-SYS-RCM-Flow-001..003
/// System-level flow tests for Recommender module.
/// TC-001: ExamEvent ingestion → Mastery created → WeakTag query reflects result.
/// TC-002: GetWeakTags without auth → 401 (controller security spot-check).
/// TC-003: GetRecommendedLectures returns remedial topics first.
/// </summary>
public sealed class RecommenderHttpFlowTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private const string StudentId = "student-rcm-sys-001";

    public RecommenderHttpFlowTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new RecommenderDbContext(options);
    }

    public void Dispose() => _db.Dispose();

    // ── TC-SYS-RCM-Flow-001 ───────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-RCM-Flow-001: UC-52 — ExamEvent → Mastery updated → GetWeakTags returns result.
    /// End-to-end: ingest a GradeCalculatedEvent → query RecommenderService → weak tag appears.
    /// </summary>
    [Fact]
    public async Task Flow_ExamEventIngestion_ThenGetWeakTags_ReturnsWeakTag()
    {
        var studentId = Guid.NewGuid();
        var tagId     = Guid.NewGuid();

        // Seed TagTopic
        _db.TagTopics.AddRange(
            new TagTopicReadOnly
            {
                TagId = "root-topic",
                TagName = "Root topic",
                Grade = 10,
                IsActive = true
            },
            new TagTopicReadOnly
        {
            TagId   = tagId.ToString(),
            ParentTagId = "root-topic",
            TagName = "Đạo hàm",
            Grade   = 10,
            IsActive = true
        });
        await _db.SaveChangesAsync();

        // Act 1: Ingest exam event (topicScore=0 → weak)
        var competencyEngine = new CompetencyEngine(_db);
        var handler          = new TopicResultIngestionHandler(_db, competencyEngine);
        var gradedEvent      = MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 0.00m);
        await handler.Handle(gradedEvent, default);

        // Act 2: Query weak tags via RecommenderService
        var service  = new RecommenderService(_db, new DifficultyMappingService());
        var weakTags = await service.GetStudentWeakTagsAsync(studentId.ToString());

        // Assert
        Assert.Single(weakTags);
        Assert.Equal(tagId.ToString(), weakTags[0].TagId);
        Assert.Equal("Đạo hàm", weakTags[0].TagName);
        Assert.True(weakTags[0].OfficialPoint < 5.00m,
            $"Expected weak but OfficialPoint={weakTags[0].OfficialPoint}");
    }

    // ── TC-SYS-RCM-Flow-002 ───────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-RCM-Flow-002: UC-52 — GetWeakTags without auth → 401.
    /// Security spot-check: controller returns 401 and handler is never invoked.
    /// </summary>
    [Fact]
    public async Task Flow_GetWeakTagsUnauthenticated_Returns401_HandlerNotCalled()
    {
        var mediator = new Mock<IMediator>();

        var controller = new RecommenderController(
            mediator.Object,
            NullLogger<RecommenderController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    // No NameIdentifier claim
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            }
        };

        var result = await controller.GetWeakTags(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
        mediator.Verify(m => m.Send(
            It.IsAny<GetWeakTagsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TC-SYS-RCM-Flow-003 ───────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-RCM-Flow-003: UC-53 — Remedial topics sorted first in lecture recommendations.
    /// Seeds 2 weak tags: remedial (level 1) and non-remedial (level 2).
    /// Verifies remedial lecture appears first in the handler-returned list.
    /// </summary>
    [Fact]
    public async Task Flow_RecommendedLectures_RemedialFirst()
    {
        var tagRemedial    = Guid.NewGuid().ToString();
        var tagNonRemedial = Guid.NewGuid().ToString();
        var difficultyBasic = Guid.NewGuid().ToString();
        var difficultyIntermediate = Guid.NewGuid().ToString();

        _db.TagTopics.AddRange(
            new TagTopicReadOnly
            {
                TagId = "root-topic",
                TagName = "Root topic",
                Grade = 10,
                IsActive = true
            },
            new TagTopicReadOnly
            {
                TagId = tagRemedial,
                ParentTagId = "root-topic",
                TagName = "Remedial Topic",
                Grade = 10,
                IsActive = true
            },
            new TagTopicReadOnly
            {
                TagId = tagNonRemedial,
                ParentTagId = "root-topic",
                TagName = "Non-Remedial Topic",
                Grade = 10,
                IsActive = true
            });
        _db.TagDifficulties.AddRange(
            new TagDifficultyReadOnly
            {
                DifficultyId = difficultyBasic,
                DifficultyName = "Basic",
                LevelValue = 1,
                IsActive = true
            },
            new TagDifficultyReadOnly
            {
                DifficultyId = difficultyIntermediate,
                DifficultyName = "Intermediate",
                LevelValue = 2,
                IsActive = true
            });

        // Remedial: level=1, OfficialPoint<3.0
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId              = Guid.NewGuid().ToString(),
            StudentId                  = StudentId,
            TagId                      = tagRemedial,
            OfficialPoint              = 1.50m,
            PracticePoint              = 1.50m,
            ExamAnchor                 = 1.50m,
            MasteryStatus              = "Learning",
            NumberDone                 = 5,
            SeriesAnswerCount          = 0,
            RecommendedDifficultyLevel = 1,
            ExamHistory                = "[]"
        });
        // Non-remedial: level=2, OfficialPoint between 3.0-5.0
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId              = Guid.NewGuid().ToString(),
            StudentId                  = StudentId,
            TagId                      = tagNonRemedial,
            OfficialPoint              = 3.50m,
            PracticePoint              = 3.50m,
            ExamAnchor                 = 3.50m,
            MasteryStatus              = "Learning",
            NumberDone                 = 5,
            SeriesAnswerCount          = 0,
            RecommendedDifficultyLevel = 2,
            ExamHistory                = "[]"
        });

        // Seed Lectures: use Title (actual property name)
        _db.Lectures.AddRange(
            new LectureReadOnly
            {
                LectureId = "lec-remedial",
                Title = "Lecture A (Remedial)",
                TagId = tagRemedial,
                DifficultyId = difficultyBasic,
                Status = "Published"
            },
            new LectureReadOnly
            {
                LectureId = "lec-nonremedial",
                Title = "Lecture B (NonRemedial)",
                TagId = tagNonRemedial,
                DifficultyId = difficultyIntermediate,
                Status = "Published"
            });

        await _db.SaveChangesAsync();

        // Use query handler directly (no RecommenderService.GetRecommendedLecturesAsync exists)
        var handler  = new GetRecommendedLecturesQueryHandler(_db);
        var lectures = await handler.Handle(new GetRecommendedLecturesQuery(StudentId), default);

        Assert.Equal(2, lectures.Count);
        Assert.Equal("WeakTopicExactDifficulty", lectures[0].Reason);
        Assert.False(lectures[0].IsDifficultyFallback);
        Assert.Equal("lec-remedial", lectures[0].LectureId);
    }

    // ── Helper ───────────────────────────────────────────────────────────────

    private static GradeCalculatedEvent MakeExamEvent(
        Guid studentId, Guid sessionId, Guid tagId, decimal topicScore)
        => new()
        {
            StudentId     = studentId.ToString(),
            SessionId     = sessionId.ToString(),
            TestId        = Guid.NewGuid().ToString(),
            GradeRevision = 1,
            TestFormat    = "Exam",
            Score         = topicScore,
            NumCorrect    = topicScore > 0 ? 1 : 0,
            NumIncorrect  = topicScore > 0 ? 0 : 1,
            NumAbandoned  = 0,
            GradedAt      = DateTime.UtcNow,
            PerTagResults =
            [
                new TopicGradeResult
                {
                    TagId        = tagId.ToString(),
                    TopicScore   = topicScore,
                    CorrectItems = topicScore > 0 ? 1 : 0,
                    TotalItems   = 1,
                    EarnedPoints = topicScore / 10m,
                    MaxPoints    = 1m
                }
            ]
        };
}
