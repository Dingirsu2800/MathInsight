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
            StudentId = studentId.ToString(),
            SessionId = sessionId.ToString(),
            TestId = Guid.NewGuid().ToString(),
            GradeRevision = 1,
            TestFormat = "Exam",
            Score = topicScore,
            NumCorrect = 1,
            NumIncorrect = 0,
            NumAbandoned = 0,
            GradedAt = DateTime.UtcNow,
            PerTagResults = [new TopicGradeResult
            {
                TagId        = tagId.ToString(),
                TopicScore   = topicScore,
                CorrectItems = 1,
                TotalItems   = 1,
                EarnedPoints = topicScore / 10m,
                MaxPoints = 1m
            }]
        };

    private static GradeCalculatedEvent MakePracticeEvent(
        Guid studentId, Guid sessionId, Guid tagId,
        bool isCorrect, byte difficultyLevel, int timeSpent = 10)
        => new()
        {
            StudentId = studentId.ToString(),
            SessionId = sessionId.ToString(),
            TestId = Guid.NewGuid().ToString(),
            GradeRevision = 1,
            TestFormat = "Practice",
            Score = isCorrect ? 10m : 0m,
            NumCorrect = isCorrect ? 1 : 0,
            NumIncorrect = isCorrect ? 0 : 1,
            GradedAt = DateTime.UtcNow,
            PerTagResults = [new TopicGradeResult
            {
                TagId        = tagId.ToString(),
                TopicScore   = isCorrect ? 10m : 0m,
                CorrectItems = isCorrect ? 1 : 0,
                TotalItems   = 1,
                EarnedPoints = isCorrect ? 1m : 0m,
                MaxPoints = 1m
            }],
            Answers = [new GradedAnswerDto
            {
                QuestionId      = Guid.NewGuid().ToString(),
                TagId           = tagId.ToString(),
                TagWeights      = [new TagWeightEntry { TagId = tagId.ToString(), Weight = 1.0m, IsPrimary = true }],
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
        // OfficialPoint = 0.7×9 + 0.3×0 (initial practice) = 6.30
        Assert.Equal(6.30m, mastery.OfficialPoint);
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

    [Fact]
    public async Task Handle_RevisedOlderExam_ReplacesMatchingSessionAndIgnoresStaleRevision()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var olderSessionId = Guid.NewGuid();

        var olderEvent = MakeExamEvent(studentId, olderSessionId, tagId, topicScore: 2.00m);
        await _handler.Handle(olderEvent, default);
        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 8.00m), default);

        var revisedResult = new TopicGradeResult
        {
            TagId = tagId.ToString(),
            TopicScore = 10m,
            CorrectItems = 1,
            TotalItems = 1,
            EarnedPoints = 1m,
            MaxPoints = 1m
        };
        await _handler.Handle(olderEvent with
        {
            GradeRevision = 2,
            GradedAt = DateTime.UtcNow.AddMinutes(1),
            PerTagResults = [revisedResult]
        }, default);

        var mastery = await _db.TagsMasteries.SingleAsync();
        Assert.Equal(8.89m, Math.Round(mastery.ExamAnchor, 2));
        var snapshot = await _db.StudentTopicSessionResults
            .SingleAsync(item => item.SessionId == olderSessionId.ToString());
        Assert.Equal(2, snapshot.GradeRevision);
        Assert.Equal(10m, snapshot.TopicScore);

        await _handler.Handle(olderEvent, default);
        Assert.Equal(2, snapshot.GradeRevision);
        Assert.Equal(10m, snapshot.TopicScore);
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
        _db.TagTopics.AddRange(
            new TagTopicReadOnly { TagId = "root-11", TagName = "Root 11", Grade = 11, IsActive = true },
            new TagTopicReadOnly { TagId = tagId.ToString(), ParentTagId = "root-11", TagName = "Child 11", Grade = 11, IsActive = true });
        await _db.SaveChangesAsync();

        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 8.00m), default);

        // CompetencyPoint should exist for grade=11 (resolved from student)
        var cp = await _db.CompetencyPoints
            .FirstOrDefaultAsync(c => c.StudentId == studentId.ToString() && c.Grade == 11);
        Assert.NotNull(cp);
        Assert.True(cp.Point >= 0m && cp.Point <= 10m);
    }

    [Fact]
    public async Task CompetencyEngine_GradeTwelveWithOnlyGradeTenMastery_DoesNotCreateGradeTwelvePoint()
    {
        const string studentId = "student-grade-12";
        _db.TagTopics.AddRange(
            new TagTopicReadOnly { TagId = "root-10", TagName = "Root 10", Grade = 10, IsActive = true },
            new TagTopicReadOnly { TagId = "child-10", ParentTagId = "root-10", TagName = "Child 10", Grade = 10, IsActive = true });
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId = "mastery-grade-10",
            StudentId = studentId,
            TagId = "child-10",
            OfficialPoint = 8m
        });
        await _db.SaveChangesAsync();

        await new CompetencyEngine(_db).RecalculateAsync(studentId, 12);

        Assert.DoesNotContain(_db.CompetencyPoints, item => item.StudentId == studentId && item.Grade == 12);
    }

    // ── Test: SQL-only recommender works without Redis/SAR ─────────────────────

    [Fact]
    public async Task CompetencyEngine_AveragesOnlyActiveDirectChildrenOfActiveSameGradeRoots()
    {
        const string studentId = "student-competency-filter";
        _db.TagTopics.AddRange(
            new TagTopicReadOnly { TagId = "root-valid", TagName = "Valid root", Grade = 12, IsActive = true },
            new TagTopicReadOnly { TagId = "child-valid", ParentTagId = "root-valid", TagName = "Valid child", Grade = 12, IsActive = true },
            new TagTopicReadOnly { TagId = "child-inactive", ParentTagId = "root-valid", TagName = "Inactive child", Grade = 12, IsActive = false },
            new TagTopicReadOnly { TagId = "root-inactive", TagName = "Inactive root", Grade = 12, IsActive = false },
            new TagTopicReadOnly { TagId = "child-inactive-parent", ParentTagId = "root-inactive", TagName = "Child inactive parent", Grade = 12, IsActive = true },
            new TagTopicReadOnly { TagId = "nested-legacy", ParentTagId = "child-valid", TagName = "Nested legacy", Grade = 12, IsActive = true },
            new TagTopicReadOnly { TagId = "root-grade-11", TagName = "Grade 11 root", Grade = 11, IsActive = true },
            new TagTopicReadOnly { TagId = "child-grade-mismatch", ParentTagId = "root-grade-11", TagName = "Grade mismatch", Grade = 12, IsActive = true });
        _db.TagsMasteries.AddRange(
            Mastery("mastery-root", studentId, "root-valid", 10m),
            Mastery("mastery-valid", studentId, "child-valid", 6m),
            Mastery("mastery-inactive-child", studentId, "child-inactive", 10m),
            Mastery("mastery-inactive-parent", studentId, "child-inactive-parent", 10m),
            Mastery("mastery-nested", studentId, "nested-legacy", 10m),
            Mastery("mastery-mismatch", studentId, "child-grade-mismatch", 10m));
        await _db.SaveChangesAsync();

        await new CompetencyEngine(_db).RecalculateAsync(studentId, 12);

        var competency = await _db.CompetencyPoints.SingleAsync(item => item.StudentId == studentId && item.Grade == 12);
        Assert.Equal(6m, competency.Point);
    }

    private static TagsMastery Mastery(string id, string studentId, string tagId, decimal officialPoint) => new()
    {
        TagsMasteryId = id,
        StudentId = studentId,
        TagId = tagId,
        OfficialPoint = officialPoint
    };

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

        // PracticePoint started at 0.00 (lazy-create), Δ=+0.05 → 0.05
        Assert.Equal(0.05m, mastery.PracticePoint);
    }

    [Fact]
    public async Task Handle_RevisedPractice_ReversesMachineDeltaWithoutDoubleCounting()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var original = MakePracticeEvent(
            studentId, sessionId, tagId, isCorrect: true, difficultyLevel: 2);
        await _handler.Handle(original, default);

        var revisedAnswers = original.Answers.Select(answer => answer with
        {
            IsCorrect = true,
            MachineIsCorrect = true,
            IsScoreInvalidated = true,
            PointsEarned = 1m
        }).ToList();
        await _handler.Handle(original with
        {
            GradeRevision = 2,
            Score = 10m,
            NumCorrect = 0,
            NumIncorrect = 0,
            GradedAt = DateTime.UtcNow.AddMinutes(1),
            Answers = revisedAnswers,
            PerTagResults =
            [
                new TopicGradeResult
                {
                    TagId = tagId.ToString(),
                    TopicScore = 0m,
                    CorrectItems = 0,
                    TotalItems = 0,
                    EarnedPoints = 0m,
                    MaxPoints = 0m
                }
            ]
        }, default);

        var mastery = await _db.TagsMasteries.SingleAsync();
        Assert.Equal(0.00m, mastery.PracticePoint);
        Assert.Equal(0, mastery.NumberDone);
        Assert.Equal(0, mastery.NumCorrect);
        Assert.Equal(0m, mastery.AccuracyRate);
        Assert.Equal(0, mastery.SeriesAnswerCount);
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

    // ── Test: Exam event preserves the PracticePoint component ────────────────

    [Fact]
    public async Task Handle_ExamEvent_PreservesExistingPracticePoint()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId = "mastery-existing-practice",
            StudentId = studentId.ToString(),
            TagId = tagId.ToString(),
            OfficialPoint = 4.00m,
            PracticePoint = 4.00m,
            ExamAnchor = 2.00m,
            MasteryStatus = "Learning",
            ExamHistory = "[]"
        });
        await _db.SaveChangesAsync();

        await _handler.Handle(
            MakeExamEvent(studentId, Guid.NewGuid(), tagId, topicScore: 9.00m), default);

        var mastery = await _db.TagsMasteries
            .FirstAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());

        // OfficialPoint = 0.7*9 + 0.3*4 = 7.50; PracticePoint remains 4.00.
        Assert.Equal(7.50m, mastery.OfficialPoint);
        Assert.Equal(4.00m, mastery.PracticePoint);
    }

    [Fact]
    public async Task Handle_PracticeEvent_PreservesExistingExamAnchor()
    {
        var studentId = Guid.NewGuid();
        var tagId = Guid.NewGuid();
        _db.TagsMasteries.Add(new TagsMastery
        {
            TagsMasteryId = "mastery-existing-exam",
            StudentId = studentId.ToString(),
            TagId = tagId.ToString(),
            OfficialPoint = 6.00m,
            PracticePoint = 4.00m,
            ExamAnchor = 7.00m,
            MasteryStatus = "Learning",
            ExamHistory = "[]"
        });
        await _db.SaveChangesAsync();

        await _handler.Handle(
            MakePracticeEvent(studentId, Guid.NewGuid(), tagId, isCorrect: true, difficultyLevel: 2), default);

        var mastery = await _db.TagsMasteries
            .FirstAsync(tm => tm.StudentId == studentId.ToString() && tm.TagId == tagId.ToString());

        Assert.Equal(7.00m, mastery.ExamAnchor);
        Assert.Equal(4.05m, mastery.PracticePoint);
        Assert.Equal(6.115m, mastery.OfficialPoint);
    }
}
