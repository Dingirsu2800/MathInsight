using MediatR;
using MathInsight.Shared.Events;

namespace MathInsight.Modules.Grading_Analytics.Handlers;

/// <summary>
/// Placeholder handler for TestSubmittedEvent (MediatR in-process).
/// 
/// NOTE: In MVP, grading runs synchronously inside the Testing submit flow.
/// This handler is kept as a thin stub; full GradeSubmittedSessionHandler
/// (Phase 2) will replace the grading logic here.
/// 
/// No MassTransit / RabbitMQ queue is used — see spec BR-18, Assumptions.
/// </summary>
public class TestSubmittedHandler : INotificationHandler<TestSubmittedEvent>
{
    // Phase 2 will inject: IGradingEngine, GradingDbContext, IMediator (for GradeCalculatedEvent)

    public Task Handle(TestSubmittedEvent notification, CancellationToken cancellationToken)
    {
        // TODO (Phase 2): Replace with real GradeSubmittedSessionHandler logic:
        //   1. Validate TestSession.Status == InProgress
        //   2. Run GradingEngine.Grade(session) synchronously
        //   3. Write TestAnswer results + update TestSession in single transaction (DC-05)
        //   4. Set TestSession.Status = Graded; persist SubmissionType
        //   5. Publish GradeCalculatedEvent (to Recommender + Notification modules)

        return Task.CompletedTask;
    }
}