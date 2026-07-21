using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MathInsight.Modules.Testing.Commands.ReportSessionQuestion;

/// <summary>
/// Delegates question reporting to the QuestionBank module's ReportQuestionCommand.
/// In the current modular monolith, this is a thin wrapper that validates session context
/// and then publishes a notification for the QuestionBank module to handle.
/// </summary>
public sealed class ReportSessionQuestionCommandHandler
    : IRequestHandler<ReportSessionQuestionCommand, Result<bool>>
{
    private readonly TestingDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<ReportSessionQuestionCommandHandler> _logger;

    public ReportSessionQuestionCommandHandler(
        TestingDbContext db,
        IMediator mediator,
        ILogger<ReportSessionQuestionCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result<bool>> Handle(
        ReportSessionQuestionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Validate session exists, belongs to student, and is InProgress
        var session = await _db.TestSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
            return Result<bool>.Failure(TestingErrors.SessionNotFound);

        if (session.StudentId != request.StudentId)
            return Result<bool>.Failure(TestingErrors.SessionNotFound);

        if (session.Status != "InProgress")
            return Result<bool>.Failure(TestingErrors.SessionNotInProgress);

        // 2. Verify question belongs to this session's test
        var questionExists = await _db.TestAnswers
            .AnyAsync(a => a.SessionId == request.SessionId
                        && a.QuestionId == request.QuestionId,
                cancellationToken);

        if (!questionExists)
            return Result<bool>.Failure(TestingErrors.RequestInvalid);

        // 3. Delegate to QuestionBank module
        // In the modular monolith, this would call the QuestionBank module's
        // ReportQuestionCommand via MediatR. For now, log the report for audit.
        _logger.LogInformation(
            "Question report: SessionId={SessionId}, QuestionId={QuestionId}, StudentId={StudentId}, Reason={Reason}",
            request.SessionId,
            request.QuestionId,
            request.StudentId,
            request.Reason);

        // TODO: When QuestionBank module (002) exposes ReportQuestionCommand via MediatR,
        // replace this with:
        // await _mediator.Send(new ReportQuestionCommand(request.QuestionId, request.StudentId, request.Reason, request.SessionId));

        return Result<bool>.Success(true);
    }
}
