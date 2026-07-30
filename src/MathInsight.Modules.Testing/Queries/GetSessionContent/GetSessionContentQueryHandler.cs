using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Queries.GetSessionContent;

public sealed class GetSessionContentQueryHandler
    : IRequestHandler<GetSessionContentQuery, Result<TestSessionViewResponse>>
{
    private readonly TestingDbContext _db;

    public GetSessionContentQueryHandler(TestingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<TestSessionViewResponse>> Handle(
        GetSessionContentQuery request,
        CancellationToken cancellationToken)
    {
        var session = await _db.TestSessions
            .AsNoTracking()
            .Include(item => item.Test)
            .FirstOrDefaultAsync(item => item.SessionId == request.SessionId, cancellationToken);

        if (session is null || !string.Equals(session.StudentId, request.StudentId, StringComparison.Ordinal))
            return Result<TestSessionViewResponse>.Failure(TestingErrors.SessionNotFound);

        var test = session.Test;
        if (test is null)
            return Result<TestSessionViewResponse>.Failure(TestingErrors.TestNotFound);

        var rows = await QuestionSnapshotReader.LoadAsync(_db, session.TestId, cancellationToken);
        var savedAnswers = await _db.TestAnswers
            .AsNoTracking()
            .Include(answer => answer.Options)
            .Include(answer => answer.Parts)
            .Where(answer => answer.SessionId == session.SessionId)
            .OrderBy(answer => answer.QuestionNo)
            .Select(answer => new SavedTestAnswerResponse(
                answer.QuestionId,
                answer.AnswerId,
                answer.ShortAnswerText,
                answer.TimeSpent,
                answer.Options
                    .OrderBy(option => option.AnswerId)
                    .Select(option => new AutoSaveOptionDto(option.AnswerId))
                    .ToList(),
                answer.Parts
                    .OrderBy(part => part.PartId)
                    .Select(part => new AutoSavePartDto(
                        part.PartId,
                        part.BooleanAnswer,
                        part.TextAnswer,
                        part.NumericAnswer))
                    .ToList()))
            .ToListAsync(cancellationToken);
        var questions = rows.Values
            .OrderBy(row => row.TestQuestion.QuestionOrder)
            .Select(row => new StudentQuestionResponse(
                row.Snapshot.QuestionId,
                row.TestQuestion.QuestionVersionId,
                row.TestQuestion.QuestionOrder,
                row.Snapshot.QuestionType,
                row.Snapshot.QuestionContent ?? row.TestQuestion.QuestionVersion?.QuestionContent ?? string.Empty,
                row.Snapshot.PictureUrl ?? row.TestQuestion.QuestionVersion?.PictureUrl,
                row.TestQuestion.MaxPointsSnapshot,
                row.Snapshot.Answers
                    .Select(answer => new StudentAnswerOptionResponse(answer.AnswerId, answer.AnswerContent))
                    .ToList(),
                row.Snapshot.Parts
                    .OrderBy(part => part.PartOrder)
                    .Select(part => new StudentQuestionPartResponse(
                        part.PartId,
                        part.PartOrder,
                        part.PartLabel,
                        part.PartContent,
                        part.PartType))
                    .ToList()))
            .ToList();

        var now = DateTime.UtcNow;
        var remainingSeconds = SessionTimePolicy.RemainingSeconds(session.StartTime, test.DurationMinutes, now);

        return Result<TestSessionViewResponse>.Success(new TestSessionViewResponse(
            session.SessionId,
            session.TestId,
            test.TestName,
            session.Status,
            session.TestFormat,
            test.DurationMinutes,
            test.MaxScore,
            remainingSeconds ?? 0,
            questions,
            savedAnswers)
        {
            HasTimeLimit = SessionTimePolicy.HasTimeLimit(test.DurationMinutes),
            RemainingSecondsNullable = remainingSeconds,
            ElapsedSeconds = SessionTimePolicy.ElapsedSeconds(session.StartTime, now)
        });
    }
}
