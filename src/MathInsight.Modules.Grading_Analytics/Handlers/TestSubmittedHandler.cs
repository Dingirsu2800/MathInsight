using MediatR;
using MassTransit;
using MathInsight.Shared.Events;

namespace MathInsight.Modules.Grading_Analytics.Handlers;

/// <summary>
/// Handles the TestSubmittedEvent synchronously in-memory (for Practice mode)
/// </summary>
public class TestSubmittedHandler : INotificationHandler<TestSubmittedEvent>
{
    private readonly IPublishEndpoint _publishEndpoint;

    public TestSubmittedHandler(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public async Task Handle(TestSubmittedEvent notification, CancellationToken cancellationToken)
    {
        // Simulating immediate grading logic...
        double score = 9.0;
        int correct = 18;
        int incorrect = 2;
        var weakTags = new List<string> { "Calculus" };

        // Save result to "grd.GradingResults" database schema

        // Publish GradeCalculated event so that the gamification and recommender modules can react
        await _publishEndpoint.Publish(new GradeCalculatedEvent
        {
            SessionId = notification.SessionId,
            StudentId = notification.StudentId,
            TestId = notification.TestId,
            Score = score,
            NumCorrect = correct,
            NumIncorrect = incorrect,
            WeakTags = weakTags,
            GradedTime = DateTime.UtcNow
        }, cancellationToken);
    }
}