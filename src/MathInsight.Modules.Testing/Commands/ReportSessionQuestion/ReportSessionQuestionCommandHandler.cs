using MathInsight.Modules.QuestionBank.Commands.ReportQuestion;
using MathInsight.Modules.QuestionBank.Contracts.Reports;
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
        // Validate ownership. Reports are allowed while taking the test or from history.
        var session = await _db.TestSessions
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
            return Result<bool>.Failure(TestingErrors.SessionNotFound);

        if (session.StudentId != request.StudentId)
            return Result<bool>.Failure(TestingErrors.SessionNotFound);

        var testQuestion = await _db.TestQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TestId == session.TestId &&
                                         item.QuestionId == request.QuestionId,
                cancellationToken);
        if (testQuestion is null)
            return Result<bool>.Failure(TestingErrors.RequestInvalid);

        var result = await _mediator.Send(new ReportQuestionCommand(
            request.QuestionId,
            new ReportQuestionRequest { ReportReason = request.Reason },
            request.StudentId,
            "Student",
            request.SessionId,
            testQuestion.QuestionVersionId), cancellationToken);
        if (result.IsFailure)
        {
            _logger.LogWarning(
                "Question report was rejected for session {SessionId}: {Code}",
                request.SessionId,
                result.Error?.Code);
            return Result<bool>.Failure(result.Error ?? TestingErrors.RequestInvalid);
        }

        return Result<bool>.Success(true);
    }
}
