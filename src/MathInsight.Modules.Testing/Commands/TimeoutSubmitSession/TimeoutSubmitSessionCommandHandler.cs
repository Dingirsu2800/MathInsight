using MathInsight.Modules.Testing.Commands.ForceSubmitSession;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Commands.TimeoutSubmitSession;

public sealed class TimeoutSubmitSessionCommandHandler
    : IRequestHandler<TimeoutSubmitSessionCommand, Result<SubmitSessionResponse>>
{
    private readonly TestingDbContext _db;
    private readonly IMediator _mediator;

    public TimeoutSubmitSessionCommandHandler(TestingDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<SubmitSessionResponse>> Handle(
        TimeoutSubmitSessionCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _db.TestSessions
            .AsNoTracking()
            .Include(item => item.Test)
            .FirstOrDefaultAsync(item => item.SessionId == request.SessionId, cancellationToken);

        if (session is null ||
            !string.Equals(session.StudentId, request.StudentId, StringComparison.Ordinal))
        {
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotFound);
        }

        if (session.Status != "InProgress")
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionAlreadyCompleted);

        if (session.Test is null)
            return Result<SubmitSessionResponse>.Failure(TestingErrors.TestNotFound);

        if (!SessionTimePolicy.HasTimeLimit(session.Test.DurationMinutes))
            return Result<SubmitSessionResponse>.Failure(TestingErrors.TestHasNoTimeLimit);

        var expiresAt = session.StartTime.AddMinutes(session.Test.DurationMinutes);
        if (DateTime.UtcNow < expiresAt)
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotExpired);

        return await _mediator.Send(
            new ForceSubmitSessionCommand(session.SessionId, "TimeoutSubmit"),
            cancellationToken);
    }
}
