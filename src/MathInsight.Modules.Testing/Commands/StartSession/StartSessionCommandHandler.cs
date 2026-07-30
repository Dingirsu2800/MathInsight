using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Entities;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace MathInsight.Modules.Testing.Commands.StartSession;

public sealed class StartSessionCommandHandler
    : IRequestHandler<StartSessionCommand, Result<StartSessionResponse>>
{
    private readonly TestingDbContext _db;

    public StartSessionCommandHandler(TestingDbContext db)
    {
        _db = db;
    }

    public Task<Result<StartSessionResponse>> Handle(
        StartSessionCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TestId) || string.IsNullOrWhiteSpace(request.StudentId))
            return Task.FromResult(Result<StartSessionResponse>.Failure(TestingErrors.RequestInvalid));

        var sessionId = Guid.NewGuid().ToString("D");
        var startTime = DateTime.UtcNow;
        return StartSessionExecutionStrategy.ExecuteAsync(
            _db,
            () => ExecuteAsync(request, sessionId, startTime, cancellationToken),
            () => VerifySucceededAsync(request, sessionId, cancellationToken),
            cancellationToken);
    }

    private async Task<Result<StartSessionResponse>> ExecuteAsync(
        StartSessionCommand request,
        string sessionId,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction? transaction = _db.Database.IsRelational()
            ? await _db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        if (TestSqlServerLock.IsSupported(_db))
            await TestSqlServerLock.LockAsync(_db, request.TestId, cancellationToken);

        var existingAttempt = await _db.TestSessions
            .AsNoTracking()
            .AnyAsync(session => session.SessionId == sessionId, cancellationToken);
        if (existingAttempt)
        {
            var verification = await VerifySucceededAsync(request, sessionId, cancellationToken);
            return verification.IsSuccessful
                ? verification.Result
                : Result<StartSessionResponse>.Failure(TestingErrors.RequestInvalid);
        }

        var test = await _db.Tests
            .Include(t => t.Questions)
            .Include(t => t.Blueprint)
            .FirstOrDefaultAsync(t => t.TestId == request.TestId, cancellationToken);

        if (test is null)
            return Result<StartSessionResponse>.Failure(TestingErrors.TestNotFound);

        if (test.TestStatus != "Active")
            return Result<StartSessionResponse>.Failure(TestingErrors.TestNotActive);

        var student = await _db.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.StudentId == request.StudentId, cancellationToken);

        if (student?.CurrentGrade is not (10 or 11 or 12))
            return Result<StartSessionResponse>.Failure(TestingErrors.TestAccessDenied);

        var hasAccess = test.GeneratedForStudentId is not null
            ? string.Equals(test.GeneratedForStudentId, request.StudentId, StringComparison.OrdinalIgnoreCase)
            : test.TestMode == "BlueprintExam"
              && test.TestStatus == "Active"
              && test.Blueprint is { Status: "Active" }
              && test.Blueprint.Grade == student.CurrentGrade;

        if (!hasAccess)
            return Result<StartSessionResponse>.Failure(TestingErrors.TestAccessDenied);

        // BR-15: Check no existing InProgress session for same (StudentID, TestID).
        var existingSession = await _db.TestSessions
            .AnyAsync(s => s.StudentId == request.StudentId
                        && s.TestId == request.TestId
                        && s.Status == "InProgress",
                cancellationToken);

        if (existingSession)
            return Result<StartSessionResponse>.Failure(TestingErrors.SessionAlreadyInProgress);

        var session = new TestSession
        {
            SessionId = sessionId,
            TestId = request.TestId,
            StudentId = request.StudentId,
            TestFormat = test.TestMode switch
            {
                "BlueprintExam" or "Diagnostic" or "MockTest" => "Exam",
                "AdaptivePractice" or "TopicPractice" or "Practice" => "Practice",
                _ => throw new InvalidOperationException($"Unsupported TestMode '{test.TestMode}' for TestFormat mapping.")
            },
            Status = "InProgress",
            SubmissionType = null,
            Duration = 0,
            StartTime = startTime,
            TotalQuestion = test.TotalQuestions,
            NumCorrect = 0,
            NumIncorrect = 0,
            NumAbandoned = 0,
            Score = 0
        };

        _db.TestSessions.Add(session);

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

        if (transaction is not null)
            await transaction.CommitAsync(cancellationToken);

        return Result<StartSessionResponse>.Success(ToResponse(session, test));
    }

    private async Task<(bool IsSuccessful, Result<StartSessionResponse> Result)> VerifySucceededAsync(
        StartSessionCommand request,
        string sessionId,
        CancellationToken cancellationToken)
    {
        var session = await _db.TestSessions
            .AsNoTracking()
            .Include(item => item.Answers)
            .Include(item => item.Test)
                .ThenInclude(test => test!.Questions)
            .FirstOrDefaultAsync(item => item.SessionId == sessionId, cancellationToken);
        var succeeded = session is not null &&
            session.Test is not null &&
            session.TestId == request.TestId &&
            session.StudentId == request.StudentId &&
            session.Status == "InProgress" &&
            session.SubmissionType is null &&
            session.TotalQuestion == session.Test.TotalQuestions &&
            session.Answers.Count == session.Test.Questions.Count &&
            session.Answers.Select(answer => answer.QuestionId).ToHashSet(StringComparer.OrdinalIgnoreCase)
                .SetEquals(session.Test.Questions.Select(question => question.QuestionId));

        return succeeded
            ? (true, Result<StartSessionResponse>.Success(ToResponse(session!, session!.Test!)))
            : (false, default!);
    }

    private static StartSessionResponse ToResponse(TestSession session, Test test)
    {
        var now = DateTime.UtcNow;
        var remainingSeconds = SessionTimePolicy.RemainingSeconds(session.StartTime, test.DurationMinutes, now);
        return new(
            SessionId: session.SessionId,
            TestId: test.TestId,
            TestFormat: session.TestFormat,
            Status: session.Status,
            StartTime: session.StartTime,
            DurationMinutes: test.DurationMinutes,
            TotalQuestions: test.TotalQuestions,
            Questions: test.Questions
                .OrderBy(question => question.QuestionOrder)
                .Select(question => new SessionQuestionDto(question.QuestionId, question.QuestionOrder))
                .ToList(),
            HasTimeLimit: SessionTimePolicy.HasTimeLimit(test.DurationMinutes),
            RemainingSeconds: remainingSeconds,
            ElapsedSeconds: SessionTimePolicy.ElapsedSeconds(session.StartTime, now));
    }
}
