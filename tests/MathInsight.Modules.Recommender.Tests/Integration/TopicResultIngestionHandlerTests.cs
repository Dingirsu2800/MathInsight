using Microsoft.EntityFrameworkCore;
using MathInsight.Modules.Recommender.Handlers;
using MathInsight.Modules.Recommender.Persistence;
using MathInsight.Modules.Recommender.Persistence.Entities;
using MathInsight.Modules.Recommender.Services;
using MathInsight.Shared.Events;
using Xunit;

namespace MathInsight.Modules.Recommender.Tests.Integration;

/// <summary>
/// Integration tests for TopicResultIngestionHandler.
/// Uses InMemory EF to simulate DB without a real SQL Server connection.
///
/// Tests:
/// - Duplicate (session_id, tag_id) does not double-update TagsMastery.
/// - Graded session inserts StudentTopicSessionResult and updates TagsMastery.
/// - TagsMastery unique key is (student_id, tag_id) only — no difficulty_id.
/// - WeakTags query returns only rows with official_point &lt; 5.00.
/// - SQL-only recommender works without Redis/SAR.
/// - CompetencyPoint is recalculated after TagsMastery update (RCM-12).
/// </summary>
public class TopicResultIngestionHandlerTests : IDisposable
{
    private readonly RecommenderDbContext _db;
    private readonly TopicResultIngestionHandler _handler;

    public TopicResultIngestionHandlerTests()
    {
        var options = new DbContextOptionsBuilder<RecommenderDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new RecommenderDbContext(options);
        var competencyEngine = new CompetencyEngine(_db);
        _handler = new TopicResultIngestionHandler(_db, competencyEngine);
    }

    public void Dispose() => _db.Dispose();

    private static GradeCalculatedEvent MakeExamEvent(
        Guid studentId, Guid sessionId, Guid tagId, decimal topicScore)
        => new()
        {
            StudentId    = studentId,
            SessionId    = sessionId,
            TestId       = Guid.NewGuid(),
            TestFormat   = "Exam",
            Score        = topicScore,
            NumCorrect   = 1,
            NumIncorrect = 0,
            NumAbandoned = 0,
            GradedAt     = DateTime.UtcNow,
            PerTagResults = [new TopicGradeResult
            {
                TagId        = tagId,
                TopicScore   = topicScore,
                CorrectCount = 1,
                TotalCount   = 1
            }]
        };

    private static GradeCalculatedEvent MakePracticeEvent(
        Guid studentId, Guid sessionId, Guid tagId,
        bool isCorrect, byte difficultyLevel, int timeSpent = 10)
        => new()
        {
            StudentId    = studentId,
            SessionId    = sessionId,
            TestId       = Guid.NewGuid(),
            TestFormat   = "Practice",
            Score        = isCorrect ? 10m : 0m,
            NumCorrect   = isCorrect ? 1 : 0,
            NumIncorrect = isCorrect ? 0 : 1,
            GradedAt     = DateTime.UtcNow,
            PerTagResults = [new TopicGradeResult
            {
                TagId        = tagId,
                TopicScore   = isCorrect ? 10m : 0m,
                CorrectCount = isCorrect ? 1 : 0,
                TotalCount   = 1
            }],
            Answers = [new GradedAnswerDto
            {
                QuestionId      = Guid.NewGuid(),
                TagId           = tagId,
                TagWeights      = [new TagWeightEntry { TagId = tagId, Weight = 1.0m, IsPrimary = true }],
                NormalizedScore = isCorrect ? 10m : 0m,
                IsCorrect       = isCorrect,
                PointsEarned    = isCorrect ? 1m : 0m,
                MaxPoints       = 1m,
                TimeSpent       = timeSpent,
                DifficultyLevel = difficultyLevel,
                QuestionNo      = 1,
                IsAbandoned     = false
            }]
        };

    // ── Test: Duplicate (session_id, tag_id) does not double-update ─────────────

    [Fact]
    public async Task Handle_DuplicateEvent_SameSessionAndTag_DoesNotDoubleUpdate()
    {
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        var evt = MakeExamEvent(studentId, sessionId, tagId, topicScore: 8.00m);

        // First handle
        await _handler.Handle(evt, default);
        var masteryAfterFirst = await _db.TagsMasteries
            .FirstAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());
        var pointAfterFirst = masteryAfterFirst.OfficialPoint;

        // Second handle — same session, same tag — must be idempotent
        await _handler.Handle(evt, default);
        var masteryAfterSecond = await _db.TagsMasteries
            .FirstAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());

        Assert.Equal(pointAfterFirst, masteryAfterSecond.OfficialPoint);

        // Also verify only 1 StudentTopicSessionResult row
        var sessionResultCount = await _db.StudentTopicSessionResults
            .CountAsync(r => r.SessionId == sessionId.ToString() && r.TagId == tagId.ToString());
        Assert.Equal(1, sessionResultCount);
    }

    // ── Test: Graded session creates StudentTopicSessionResult and updates TagsMastery ──

    [Fact]
    public async Task Handle_ExamEvent_InsertsSessionResultAndUpdatesMastery()
    {
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        await _handler.Handle(
            MakeExamEvent(studentId, sessionId, tagId, topicScore: 9.00m), default);

        // StudentTopicSessionResult should be inserted
        var sessionResult = await _db.StudentTopicSessionResults
            .FirstOrDefaultAsync(r => r.SessionId == sessionId.ToString() && r.TagId == tagId.ToString());
        Assert.NotNull(sessionResult);
        Assert.Equal(9.00m, sessionResult.TopicScore);

        // TagsMastery should be lazy-created and updated
        var mastery = await _db.TagsMasteries
            .FirstOrDefaultAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());
        Assert.NotNull(mastery);
        // ExamAnchor after one exam result = T1 = 9.00
        Assert.Equal(9.00m, mastery.ExamAnchor);
        // OfficialPoint = 0.7×9 + 0.3×5 (initial practice) = 6.3 + 1.5 = 7.8
        Assert.Equal(7.80m, mastery.OfficialPoint);
    }

    // ── Test: TagsMastery unique key is (student_id, tag_id) only ──────────────

    [Fact]
    public async Task Handle_MultipleExamSessions_SameStudentAndTag_UpsertsSingleMasteryRow()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        // Two different sessions for same student + tag
        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 7.00m), default);
        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 9.00m), default);

        var masteryCount = await _db.TagsMasteries
            .CountAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());

        // Unique key: only 1 row must exist
        Assert.Equal(1, masteryCount);
    }

    // ── Test: WeakTags query returns only rows with official_point < 5.00 ────

    [Fact]
    public async Task Handle_WeakPoint_IsReturnedByWeakTagQuery()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        // Exam score 0 → ExamAnchor=0, OfficialPoint= 0.7×0 + 0.3×5 = 1.5 → weak
        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 0.00m), default);

        var mastery = await _db.TagsMasteries
            .FirstAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());

        Assert.True(mastery.OfficialPoint < 5.00m, $"Expected weak but got {mastery.OfficialPoint}");
    }

    [Fact]
    public async Task Handle_StrongPoint_IsNotReturnedByWeakTagQuery()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        // Exam score 10 → ExamAnchor=10, OfficialPoint= 0.7×10 + 0.3×5 = 8.5 → not weak
        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 10.00m), default);

        var mastery = await _db.TagsMasteries
            .FirstAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());

        Assert.False(mastery.OfficialPoint < 5.00m, $"Expected not weak but got {mastery.OfficialPoint}");
    }

    // ── Test: CompetencyPoint is recalculated after TagsMastery update (RCM-12) ─

    [Fact]
    public async Task Handle_AfterMasteryUpdate_RecalculatesCompetencyPoint()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        // Seed student with grade 11 to test grade resolution
        _db.Students.Add(new StudentReadOnly { StudentId = studentId.ToString(), CurrentGrade = 11 });
        await _db.SaveChangesAsync();

        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 8.00m), default);

        // CompetencyPoint should exist for grade=11 (resolved from student)
        var cp = await _db.CompetencyPoints
            .FirstOrDefaultAsync(c => c.StudentId == studentId.ToString() && c.Grade == 11);
        Assert.NotNull(cp);
        Assert.True(cp.Point >= 0m && cp.Point <= 10m);
    }

    // ── Test: SQL-only recommender works without Redis/SAR ─────────────────────

    [Fact]
    public async Task Handle_WorksWithoutExternalDependencies()
    {
        // This test verifies the handler runs end-to-end with only InMemory EF.
        // No Redis, no SAR, no RabbitMQ. This is the MVP path.
        var studentId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        var exception = await Record.ExceptionAsync(() =>
            _handler.Handle(
                MakePracticeEvent(studentId, sessionId, tagId, isCorrect: true, difficultyLevel: 2), default));

        Assert.Null(exception);

        var mastery = await _db.TagsMasteries
            .FirstOrDefaultAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());
        Assert.NotNull(mastery);
    }

    // ── Test: Practice answers update PracticePoint sequentially ───────────────

    [Fact]
    public async Task Handle_PracticeEvent_UpdatesPracticePointSequentially()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();

        // One correct answer at level 2 → Δ = +0.05
        await _handler.Handle(
            MakePracticeEvent(studentId, sessionId, tagId, isCorrect: true, difficultyLevel: 2), default);

        var mastery = await _db.TagsMasteries
            .FirstAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());

        // PracticePoint started at 5.00 (lazy-create), Δ=+0.05 → 5.05
        Assert.Equal(5.05m, mastery.PracticePoint);
    }

    // ── Test: Lecture/material recommendations prioritize remedial weak topics ─

    [Fact]
    public async Task Handle_RemedialWeakTopic_HasLevel1DifficultyRecommendation()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();

        // Exam score 0 → OfficialPoint ≈ 1.5 < 3 → level 1 → remedial
        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 0.00m), default);

        var mastery = await _db.TagsMasteries
            .FirstAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());

        Assert.Equal(1, mastery.RecommendedDifficultyLevel);
        Assert.True(mastery.OfficialPoint < 5.00m);
    }
}
