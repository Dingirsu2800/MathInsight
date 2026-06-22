using MassTransit;
using MediatR;
using MathInsight.Shared.Events;

namespace MathInsight.Modules.Grading_Analytics.Consumers;

/// <summary>
/// Consumes the TestSubmittedEvent asynchronously from RabbitMQ (for Exam mode)
/// </summary>
public class TestSubmittedConsumer : IConsumer<TestSubmittedEvent>
{
    private readonly IMediator _mediator;

    public TestSubmittedConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<TestSubmittedEvent> context)
    {
        // Grade the test asynchronously
        var submission = context.Message;
        
        // Simulating grading logic...
        double score = 8.5; 
        int correct = 17;
        int incorrect = 3;
        var weakTags = new List<string> { "Trigonometry", "EquationFamily" };

        // Save result to "grd.GradingResults" database schema
        
        // Publish integration event to notify other modules (e.g. Recommendations, Notifications)
        await context.Publish(new GradeCalculatedEvent
        {
            SessionId = submission.SessionId,
            StudentId = submission.StudentId,
            TestId = submission.TestId,
            Score = score,
            NumCorrect = correct,
            NumIncorrect = incorrect,
            WeakTags = weakTags,
            GradedTime = DateTime.UtcNow
        });
    }
}