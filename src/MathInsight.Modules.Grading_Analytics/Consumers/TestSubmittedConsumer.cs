using MassTransit;
using MediatR;
using Microsoft.Extensions.Logging;
using MathInsight.Shared.Events;
using MathInsight.Modules.Grading_Analytics.Services;

namespace MathInsight.Modules.Grading_Analytics.Consumers;

/// <summary>
/// MassTransit consumer for asynchronous Exam mode grading.
///
/// When a student submits an Exam, the Testing module publishes <see cref="TestSubmittedEvent"/>
/// to the message queue (RabbitMQ or InMemory) instead of using MediatR in-process.
/// This consumer receives the message and delegates grading to <see cref="IGradingOrchestrator"/>.
///
/// Benefits:
///   - Decouples the HTTP request from grading (student gets 202 Accepted immediately)
///   - Handles bursty Exam submissions (100+ students submitting at timer expiry)
///   - MassTransit provides automatic retry/redelivery on transient failures
///
/// Idempotency:
///   The orchestrator checks session.Status == InProgress before grading. If the
///   message is redelivered after grading, it will be safely skipped.
///
/// After grading, publishes <see cref="GradeCalculatedEvent"/> via MediatR for
/// downstream in-process consumers (Recommender, Notification).
/// </summary>
public class TestSubmittedConsumer : IConsumer<TestSubmittedEvent>
{
    private readonly IGradingOrchestrator _orchestrator;
    private readonly IPublisher _publisher;
    private readonly ILogger<TestSubmittedConsumer> _logger;

    public TestSubmittedConsumer(
        IGradingOrchestrator orchestrator,
        IPublisher publisher,
        ILogger<TestSubmittedConsumer> logger)
    {
        _orchestrator = orchestrator;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TestSubmittedEvent> context)
    {
        var notification = context.Message;

        _logger.LogInformation(
            "TestSubmittedConsumer received message for session {SessionId} (TestFormat={TestFormat})",
            notification.SessionId, notification.TestFormat);

        var gradeEvent = await _orchestrator.GradeSessionAsync(
            notification.SessionId, notification, context.CancellationToken);

        // ── G3: Publish GradeCalculatedEvent via MediatR for in-process consumers ──
        if (gradeEvent is not null)
        {
            await _publisher.Publish(gradeEvent, context.CancellationToken);

            _logger.LogInformation(
                "GradeCalculatedEvent published for session {SessionId} via consumer " +
                "(Score={Score}, Correct={NumCorrect}, Incorrect={NumIncorrect}, Abandoned={NumAbandoned})",
                gradeEvent.SessionId, gradeEvent.Score,
                gradeEvent.NumCorrect, gradeEvent.NumIncorrect, gradeEvent.NumAbandoned);
        }
        else
        {
            _logger.LogWarning(
                "Grading returned null for session {SessionId}. " +
                "Session may not exist or is not InProgress.",
                notification.SessionId);
        }
    }
}
