using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Queries.GetInProgressSession;

public sealed class GetInProgressSessionQueryHandler
    : IRequestHandler<GetInProgressSessionQuery, Result<string>>
{
    private readonly TestingDbContext _db;

    public GetInProgressSessionQueryHandler(TestingDbContext db)
    {
        _db = db;
    }

    public async Task<Result<string>> Handle(
        GetInProgressSessionQuery request,
        CancellationToken cancellationToken)
    {
        var sessionId = await _db.TestSessions
            .AsNoTracking()
            .Where(session =>
                session.TestId == request.TestId &&
                session.StudentId == request.StudentId &&
                session.Status == "InProgress")
            .Select(session => session.SessionId)
            .FirstOrDefaultAsync(cancellationToken);

        return sessionId is null
            ? Result<string>.Failure(TestingErrors.SessionNotFound)
            : Result<string>.Success(sessionId);
    }
}
