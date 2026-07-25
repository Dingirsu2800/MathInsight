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

        return Result<TestSessionViewResponse>.Success(new TestSessionViewResponse(
            session.SessionId,
            session.TestId,
            test.TestName,
            session.Status,
            test.DurationMinutes,
            test.MaxScore,
            questions));
    }
}
