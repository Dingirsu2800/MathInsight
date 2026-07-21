using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Events;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Commands.ForceSubmitSession;

public sealed class ForceSubmitSessionCommandHandler
    : IRequestHandler<ForceSubmitSessionCommand, Result<SubmitSessionResponse>>
{
    private readonly TestingDbContext _db;
    private readonly IMediator _mediator;

    public ForceSubmitSessionCommandHandler(TestingDbContext db, IMediator mediator)
    {
        _db = db;
        _mediator = mediator;
    }

    public async Task<Result<SubmitSessionResponse>> Handle(
        ForceSubmitSessionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Load session
        var session = await _db.TestSessions
            .Include(s => s.Test)
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotFound);

        // 2. Validate Status = InProgress
        if (session.Status != "InProgress")
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotInProgress);

        // 3. Set EndTime and SubmissionType
        var now = DateTime.UtcNow;
        session.EndTime = now;
        session.SubmissionType = request.SubmissionType; // TimeoutSubmit or SystemSubmit
        session.Duration = (int)(now - session.StartTime).TotalSeconds;

        // 4. Count abandoned answers (BR-16b)
        session.NumAbandoned = await CountAbandonedAnswers(request.SessionId, cancellationToken);

        // 5. Grading based on test format
        var isPractice = string.Equals(session.TestFormat, "Practice", StringComparison.OrdinalIgnoreCase);

        var submissionEvent = new TestSubmittedEvent
        {
            SessionId = Guid.Parse(session.SessionId),
            StudentId = Guid.Parse(session.StudentId),
            TestId = Guid.Parse(session.TestId),
            TestFormat = session.TestFormat,
            SubmittedTime = now
        };

        if (isPractice)
        {
            // Practice mode: invoke Grading via MediatR in-process
            await _mediator.Publish(submissionEvent, cancellationToken);
            await _db.Entry(session).ReloadAsync(cancellationToken);
        }
        else
        {
            // Exam mode: publish to MassTransit queue for async grading
            await _mediator.Publish(submissionEvent, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        return Result<SubmitSessionResponse>.Success(
            new SubmitSessionResponse(
                SessionId: session.SessionId,
                Status: session.Status,
                SubmissionType: session.SubmissionType,
                NumAbandoned: session.NumAbandoned,
                Score: isPractice ? session.Score : null));
    }

    private async Task<int> CountAbandonedAnswers(
        string sessionId,
        CancellationToken cancellationToken)
    {
        var answers = await _db.TestAnswers
            .Include(a => a.Options)
            .Include(a => a.Parts)
            .Where(a => a.SessionId == sessionId)
            .ToListAsync(cancellationToken);

        int abandoned = 0;

        foreach (var answer in answers)
        {
            bool isAbandoned;

            if (answer.Parts.Count > 0)
            {
                isAbandoned = answer.Parts.All(p =>
                    p.BooleanAnswer is null &&
                    string.IsNullOrWhiteSpace(p.TextAnswer) &&
                    p.NumericAnswer is null);
            }
            else if (answer.Options.Count > 0)
            {
                isAbandoned = false;
            }
            else if (!string.IsNullOrWhiteSpace(answer.ShortAnswerText))
            {
                isAbandoned = false;
            }
            else if (answer.AnswerId is not null)
            {
                isAbandoned = false;
            }
            else
            {
                isAbandoned = true;
            }

            if (isAbandoned) abandoned++;
        }

        return abandoned;
    }
}
