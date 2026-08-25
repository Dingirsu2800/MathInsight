using MassTransit;
using MathInsight.Modules.Testing.Contracts;
using MathInsight.Modules.Testing.Commands.ForceSubmitSession;
using MathInsight.Modules.Testing.Entities;
using MathInsight.Modules.Testing.Errors;
using MathInsight.Modules.Testing.Persistence;
using MathInsight.Shared.Events;
using MathInsight.Shared.Results;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MathInsight.Modules.Testing.Commands.SubmitSession;

public sealed class SubmitSessionCommandHandler
    : IRequestHandler<SubmitSessionCommand, Result<SubmitSessionResponse>>
{
    private readonly TestingDbContext _db;
    private readonly IMediator _mediator;
    private readonly IPublishEndpoint? _publishEndpoint;

    public SubmitSessionCommandHandler(
        TestingDbContext db,
        IMediator mediator,
        IPublishEndpoint? publishEndpoint = null)
    {
        _db = db;
        _mediator = mediator;
        _publishEndpoint = publishEndpoint;
    }

    public async Task<Result<SubmitSessionResponse>> Handle(
        SubmitSessionCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Load session with answers
        var session = await _db.TestSessions
            .Include(s => s.Test)
            .FirstOrDefaultAsync(s => s.SessionId == request.SessionId, cancellationToken);

        if (session is null)
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotFound);

        if (session.StudentId != request.StudentId)
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionNotFound);

        // 2. DC-03: Validate Status = InProgress
        if (session.Status != "InProgress")
            return Result<SubmitSessionResponse>.Failure(TestingErrors.SessionAlreadyCompleted);

        var now = DateTime.UtcNow;
        if (session.Test is not null &&
            SessionTimePolicy.IsExpired(session.StartTime, session.Test.DurationMinutes, now))
        {
            return await _mediator.Send(
                new ForceSubmitSessionCommand(session.SessionId, "TimeoutSubmit"),
                cancellationToken);
        }

        var savedAnswers = await _db.TestAnswers
            .Include(answer => answer.Options)
            .Include(answer => answer.Parts)
            .Where(answer => answer.SessionId == request.SessionId)
            .ToListAsync(cancellationToken);
        var snapshots = await QuestionSnapshotReader.LoadAsync(
            _db,
            session.TestId,
            cancellationToken);
        if (savedAnswers.Any(answer =>
                !snapshots.TryGetValue(answer.QuestionId, out var snapshot) ||
                !QuestionSnapshotReader.IsValid(snapshot, answer)))
        {
            return Result<SubmitSessionResponse>.Failure(TestingErrors.AnswerNotInVersion);
        }

        if (savedAnswers.Any(answer =>
                snapshots.TryGetValue(answer.QuestionId, out var snapshot) &&
                !QuestionSnapshotReader.HasValidNumericShortAnswers(snapshot, answer)))
        {
            return Result<SubmitSessionResponse>.Failure(TestingErrors.ShortAnswerNumericRequired);
        }

        // Submission timestamps and type are persisted atomically by Grading.
        // 4. Count abandoned questions (BR-16b)
        session.NumAbandoned = await CountAbandonedAnswers(request.SessionId, cancellationToken);

        // 5. Dual-path grading based on TestFormat
        var testFormat = session.TestFormat;
        var isPractice = string.Equals(testFormat, "Practice", StringComparison.OrdinalIgnoreCase);

        if (isPractice)
        {
            // BR-16: Practice mode — invoke Grading via MediatR in-process
            // Commit only after grading updates status = Graded
            var submissionEvent = new TestSubmittedEvent
            {
                SessionId = session.SessionId,
                StudentId = session.StudentId,
                TestId = session.TestId,
                TestFormat = "Practice",
                SubmissionType = "StudentSubmit",
                SubmittedTime = now
            };

            // Grading module's handler will update session.Status = Graded
            // and populate score fields
            await _mediator.Publish(submissionEvent, cancellationToken);

            // Reload session to get updated grading fields
            await _db.Entry(session).ReloadAsync(cancellationToken);
        }
        else
        {
            // BR-17: Exam mode — publish TestSubmittedEvent for async grading.
            // DB constraints prevent setting SubmissionType while Status='InProgress',
            // so we delegate ALL state changes to GradingOrchestrator which sets
            // Status=Graded, SubmissionType, EndTime, Score atomically.
            _db.Entry(session).State = EntityState.Unchanged;

            var submissionEvent = new TestSubmittedEvent
            {
                SessionId = session.SessionId,
                StudentId = session.StudentId,
                TestId = session.TestId,
                TestFormat = "Exam",
                SubmissionType = "StudentSubmit",
                SubmittedTime = now
            };

            if (_publishEndpoint is not null)
            {
                await _publishEndpoint.Publish(submissionEvent, cancellationToken);
            }
            else
            {
                await _mediator.Publish(submissionEvent, cancellationToken);
            }
        }

        // Only save if there are tracked changes (Practice mode after grading)
        if (_db.ChangeTracker.HasChanges())
            await _db.SaveChangesAsync(cancellationToken);

        return Result<SubmitSessionResponse>.Success(
            new SubmitSessionResponse(
                SessionId: session.SessionId,
                Status: session.Status,
                SubmissionType: session.SubmissionType ?? "StudentSubmit",
                NumAbandoned: session.NumAbandoned,
                Score: isPractice ? session.Score : null));
    }

    /// <summary>
    /// Counts abandoned/unanswered questions per BR-16b rules:
    /// - SINGLE_CHOICE/TRUE_FALSE: answer_id IS NULL
    /// - MULTIPLE_SELECT: No options selected
    /// - SHORT_ANSWER: short_answer_text is null or whitespace
    /// - COMPOSITE: All child parts are unanswered
    /// </summary>
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
                // COMPOSITE: all child parts must be unanswered
                isAbandoned = answer.Parts.All(p =>
                    p.BooleanAnswer is null &&
                    string.IsNullOrWhiteSpace(p.TextAnswer) &&
                    p.NumericAnswer is null);
            }
            else if (answer.Options.Count > 0)
            {
                // Has options → MULTIPLE_SELECT: not abandoned since options exist
                isAbandoned = false;
            }
            else if (!string.IsNullOrWhiteSpace(answer.ShortAnswerText))
            {
                // SHORT_ANSWER with content → not abandoned
                isAbandoned = false;
            }
            else if (answer.AnswerId is not null)
            {
                // SINGLE_CHOICE/TRUE_FALSE with selection → not abandoned
                isAbandoned = false;
            }
            else
            {
                // No answer_id, no options, no short_answer, no parts → abandoned
                isAbandoned = true;
            }

            if (isAbandoned) abandoned++;
        }

        return abandoned;
    }
}
