using MassTransit;
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
    private readonly IPublishEndpoint? _publishEndpoint;

    public ForceSubmitSessionCommandHandler(
        TestingDbContext db,
        IMediator mediator,
        IPublishEndpoint? publishEndpoint = null)
    {
        _db = db;
        _mediator = mediator;
        _publishEndpoint = publishEndpoint;
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

        // 2. Validate Status = InProgress or already Submitted/Graded (idempotent)
        if (session.Status is not "InProgress" and not "Submitted" and not "Graded")
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotInProgress);

        // 2b. If already Graded, return success directly (idempotent retry)
        if (session.Status == "Graded")
        {
            return Result<SubmitSessionResponse>.Success(
                new SubmitSessionResponse(
                    SessionId: session.SessionId,
                    Status: session.Status,
                    SubmissionType: session.SubmissionType ?? request.SubmissionType,
                    NumAbandoned: session.NumAbandoned,
                    Score: null));
        }

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
            SessionId = session.SessionId,
            StudentId = session.StudentId,
            TestId = session.TestId,
            TestFormat = session.TestFormat,
            SubmissionType = request.SubmissionType,
            SubmittedTime = now
        };

        if (isPractice)
        {
            // Practice mode: invoke Grading via MediatR in-process (synchronous)
            await _mediator.Publish(submissionEvent, cancellationToken);
            await _db.Entry(session).ReloadAsync(cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            // Exam mode: save Status = "Submitted" FIRST to prevent race conditions
            // and check constraint violations, then publish for async grading.
            session.Status = "Submitted";
            await _db.SaveChangesAsync(cancellationToken);

            if (_publishEndpoint is not null)
            {
                await _publishEndpoint.Publish(submissionEvent, cancellationToken);
            }
            else
            {
                await _mediator.Publish(submissionEvent, cancellationToken);
            }
        }

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
