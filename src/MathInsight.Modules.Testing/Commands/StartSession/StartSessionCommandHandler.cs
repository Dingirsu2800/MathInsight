using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Entities;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Commands.StartSession;

public sealed class StartSessionCommandHandler
    : IRequestHandler<StartSessionCommand, Result<StartSessionResponse>>
{
    private readonly TestingDbContext _db;

    public StartSessionCommandHandler(TestingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<StartSessionResponse>> Handle(
        StartSessionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate test exists and is ACTIVE
        var test = await _db.Tests
            .Include(t => t.Questions)
            .FirstOrDefaultAsync(t => t.TestId == request.TestId, cancellationToken);

        if (test is null)
            return Result<StartSessionResponse>.Failure(TestingErrors.TestNotFound);

        if (!string.Equals(test.TestStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            return Result<StartSessionResponse>.Failure(TestingErrors.TestNotActive);

        // 2. BR-15: Check no existing InProgress session for same (StudentID, TestID)
        var existingSession = await _db.TestSessions
            .AnyAsync(s => s.StudentId == request.StudentId
                        && s.TestId == request.TestId
                        && s.Status == "InProgress",
                cancellationToken);

        if (existingSession)
            return Result<StartSessionResponse>.Failure(TestingErrors.SessionAlreadyInProgress);

        // 3. Create TestSession with Status = InProgress
        var sessionId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var session = new TestSession
        {
            SessionId = sessionId,
            TestId = request.TestId,
            StudentId = request.StudentId,
            TestFormat = test.TestMode switch
            {
                "BlueprintExam" => "Exam",
                "Diagnostic" => "Exam",
                "AdaptivePractice" => "Practice",
                "TopicPractice" => "Practice",
                _ => "Practice"
            },
            Status = "InProgress",
            SubmissionType = null,
            Duration = 0,
            StartTime = now,
            TotalQuestion = test.TotalQuestions,
            NumCorrect = 0,
            NumIncorrect = 0,
            NumAbandoned = 0,
            Score = 0
        };

        _db.TestSessions.Add(session);

        // 4. Create TestAnswer stub records for each TestQuestion
        var questions = test.Questions
            .OrderBy(q => q.QuestionOrder)
            .ToList();

        foreach (var tq in questions)
        {
            var answer = new TestAnswer
            {
                TestAnswerId = Guid.NewGuid().ToString(),
                SessionId = sessionId,
                QuestionId = tq.QuestionId,
                QuestionNo = tq.QuestionOrder,
                PointsEarned = 0.00m
            };

            _db.TestAnswers.Add(answer);
        }

        await _db.SaveChangesAsync(cancellationToken);

        // 5. Return session with question list
        var questionDtos = questions
            .Select(q => new SessionQuestionDto(q.QuestionId, q.QuestionOrder))
            .ToList();

        var response = new StartSessionResponse(
            SessionId: sessionId,
            TestId: test.TestId,
            TestFormat: session.TestFormat,
            Status: session.Status,
            StartTime: now,
            DurationMinutes: test.DurationMinutes,
            TotalQuestions: test.TotalQuestions,
            Questions: questionDtos);

        return Result<StartSessionResponse>.Success(response);
    }
}
