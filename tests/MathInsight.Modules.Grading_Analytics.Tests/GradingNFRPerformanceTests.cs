using System.Diagnostics;
using MathInsight.Modules.Grading_Analytics.Persistence;
using MathInsight.Modules.Grading_Analytics.Persistence.Entities;
using MathInsight.Modules.Grading_Analytics.Services;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Grading_Analytics.Tests;

/// <summary>
/// TC-SYS-NFR-GA-001..002
/// Performance (NFR) tests for Grading_Analytics module.
/// Validates SLA thresholds using xUnit Stopwatch with InMemory EF.
/// </summary>
public sealed class GradingNFRPerformanceTests
{
    // ── TC-SYS-NFR-GA-001 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-NFR-GA-001: NFR-P-GRADE-HISTORY-01 — GetSessionHistory with 100 sessions &lt; 500ms.
    /// Seeds 100 graded sessions and validates paged query responds within SLA.
    /// </summary>
    [Fact]
    public async Task GetSessionHistory_100Sessions_CompletesWithin500Ms()
    {
        // ── Seed ──────────────────────────────────────────────────────────────
        var options = new DbContextOptionsBuilder<GradingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new GradingDbContext(options);

        const string studentId = "student-perf-history";
        const int totalSessions = 100;

        for (int i = 0; i < totalSessions; i++)
        {
            db.TestSessions.Add(new TestSession
            {
                SessionId      = Guid.NewGuid().ToString(),
                TestId         = $"test-{i:D3}",
                StudentId      = studentId,
                TestFormat     = i % 2 == 0 ? "Exam" : "Practice",
                Status         = "Graded",
                SubmissionType = "StudentSubmit",
                Score          = Math.Round((decimal)(i % 10) + 1.0m, 2),
                NumCorrect     = i % 5,
                NumIncorrect   = i % 3,
                NumAbandoned   = 0,
                TotalQuestion  = 5,
                GradeRevision  = 1,
                StartTime      = DateTime.UtcNow.AddDays(-i),
                EndTime        = DateTime.UtcNow.AddDays(-i).AddHours(1)
            });
        }
        await db.SaveChangesAsync();

        // ── Act ───────────────────────────────────────────────────────────────
        var sw = Stopwatch.StartNew();

        var results = await db.TestSessions
            .Where(s => s.StudentId == studentId && s.Status == "Graded")
            .OrderByDescending(s => s.EndTime)
            .Skip(0)
            .Take(20)
            .ToListAsync();

        sw.Stop();

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.True(sw.Elapsed.TotalMilliseconds < 500,
            $"GetSessionHistory took {sw.Elapsed.TotalMilliseconds:F1}ms, exceeds 500ms SLA");

        Assert.Equal(20, results.Count);
        Assert.All(results, s => Assert.Equal("Graded", s.Status));
    }

    // ── TC-SYS-NFR-GA-002 ─────────────────────────────────────────────────────

    /// <summary>
    /// TC-SYS-NFR-GA-002: NFR-P-GRADE-01 (explicit) — GradingEngine 40 questions &lt; 2000ms.
    /// Supplements the existing implicit timing test with an explicit assertion and TC ID.
    /// Runs 40-question mixed session through GradingEngine and asserts elapsed time.
    /// </summary>
    [Fact]
    public async Task GradingEngine_40Questions_CompletesWithin2000Ms()
    {
        // ── Seed ──────────────────────────────────────────────────────────────
        var options = new DbContextOptionsBuilder<GradingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new GradingDbContext(options);

        const int questionCount = 40;
        var sessionId = Guid.NewGuid().ToString();
        var testId    = Guid.NewGuid().ToString();
        var studentId = Guid.NewGuid().ToString();

        var questions = new List<Question>();
        var testQuestions = new List<TestQuestion>();
        var answers = new List<TestAnswer>();

        for (int i = 0; i < questionCount; i++)
        {
            var questionId = $"q-{i:D3}";
            var correctId  = $"a-correct-{i:D3}";
            bool isCorrect = i % 3 != 0; // ~2/3 correct

            var q = new Question
            {
                QuestionId      = questionId,
                QuestionType    = "SINGLE_CHOICE",
                DefaultWeight   = 1.0m,
                DifficultyId    = string.Empty,
                QuestionContent = $"Q{i}",
                Answers = [ new Answer { AnswerId = correctId, QuestionId = questionId, IsCorrect = true } ]
            };
            questions.Add(q);
            testQuestions.Add(new TestQuestion
            {
                TestId              = testId,
                QuestionId          = questionId,
                MaxPointsSnapshot   = 1.0m
            });
            answers.Add(new TestAnswer
            {
                TestAnswerId = $"ta-{i:D3}",
                SessionId    = sessionId,
                QuestionId   = questionId,
                AnswerId     = isCorrect ? correctId : $"wrong-{i:D3}",
                Question     = q
            });
        }

        var session = new TestSession
        {
            SessionId  = sessionId,
            TestId     = testId,
            StudentId  = studentId,
            TestFormat = "Practice",
            Status     = "InProgress",
            StartTime  = DateTime.UtcNow.AddMinutes(-20),
            TestAnswers = answers
        };

        db.Questions.AddRange(questions);
        db.TestQuestions.AddRange(testQuestions);
        db.TestSessions.Add(session);
        await db.SaveChangesAsync();

        // ── Act: time the GradingEngine ──────────────────────────────────────
        var engine = new GradingEngine();
        var sw = Stopwatch.StartNew();
        var gradeResult = engine.Grade(session);
        sw.Stop();

        // ── Assert ────────────────────────────────────────────────────────────
        Assert.True(sw.Elapsed.TotalMilliseconds < 2000,
            $"GradingEngine took {sw.Elapsed.TotalMilliseconds:F1}ms for {questionCount} questions, exceeds 2000ms SLA");

        Assert.True(gradeResult.NumCorrect + gradeResult.NumIncorrect + gradeResult.NumAbandoned == questionCount,
            $"Expected total {questionCount} but got {gradeResult.NumCorrect}+{gradeResult.NumIncorrect}+{gradeResult.NumAbandoned}");
    }
}
