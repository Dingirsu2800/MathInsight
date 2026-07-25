using MediatR;
using Microsoft.Extensions.Logging;
using MathInsight.Shared.Events;
using MathInsight.Modules.Grading_Analytics.Services;

namespace MathInsight.Modules.Grading_Analytics.Handlers;

/// <summary>
/// In-process MediatR notification handler for Practice mode grading.
/// Called synchronously during the Testing submit flow via TestSubmittedEvent.
///
/// Delegates all grading logic to <see cref="IGradingOrchestrator"/> and publishes
/// <see cref="GradeCalculatedEvent"/> after successful grading.
///
/// For Exam mode, the equivalent flow is handled asynchronously by
/// <see cref="Consumers.TestSubmittedConsumer"/> via MassTransit.
///
/// SLA: Practice &lt; 2.0s.
/// </summary>
public class GradeSubmittedSessionHandler : INotificationHandler<TestSubmittedEvent>
{
    private readonly IGradingOrchestrator _orchestrator;
    private readonly IPublisher _publisher;
    private readonly ILogger<GradeSubmittedSessionHandler> _logger;

    public GradeSubmittedSessionHandler(
        IGradingOrchestrator orchestrator,
        IPublisher publisher,
        ILogger<GradeSubmittedSessionHandler> logger)
    {
        _orchestrator = orchestrator;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(TestSubmittedEvent notification, CancellationToken cancellationToken)
    {
        var gradeEvent = await _orchestrator.GradeSessionAsync(
            notification.SessionId, notification, cancellationToken);

        // ── G3: Publish GradeCalculatedEvent AFTER commit ─────────────────────────
        if (gradeEvent is not null)
        {
            await _publisher.Publish(gradeEvent, cancellationToken);

            _logger.LogInformation(
                "GradeCalculatedEvent published for session {SessionId} (Score={Score}, " +
                "Correct={NumCorrect}, Incorrect={NumIncorrect}, Abandoned={NumAbandoned})",
                gradeEvent.SessionId, gradeEvent.Score,
                gradeEvent.NumCorrect, gradeEvent.NumIncorrect, gradeEvent.NumAbandoned);
        }
    }
}
