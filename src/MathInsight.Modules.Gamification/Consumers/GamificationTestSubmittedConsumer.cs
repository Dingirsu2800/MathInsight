using MassTransit;
using MathInsight.Shared.Events;
using MediatR;

namespace MathInsight.Modules.Gamification.Consumers;

public class GamificationTestSubmittedConsumer : IConsumer<TestSubmittedEvent>
{
    private readonly IMediator _mediator;

    public GamificationTestSubmittedConsumer(IMediator mediator)
    {
        _mediator = mediator;
    }

    public async Task Consume(ConsumeContext<TestSubmittedEvent> context)
    {
        // Forward the MassTransit event to the internal MediatR bus
        // so that TestSubmittedHandler (and any other INotificationHandlers) can process it.
        await _mediator.Publish(context.Message, context.CancellationToken);
    }
}
