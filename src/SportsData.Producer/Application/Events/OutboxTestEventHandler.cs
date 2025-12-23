using MassTransit;
using SportsData.Core.Eventing.Events;

namespace SportsData.Producer.Application.Events;

/// <summary>
/// Handler for OutboxTestEvent in Producer service.
/// This validates that Producer can consume events it publishes (self-consumption).
/// </summary>
public class OutboxTestEventHandler : IConsumer<OutboxTestEvent>
{
    private readonly ILogger<OutboxTestEventHandler> _logger;

    public OutboxTestEventHandler(ILogger<OutboxTestEventHandler> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OutboxTestEvent> context)
    {
        var evt = context.Message;
        
        _logger.LogInformation(
            "? [PRODUCER] OutboxTestEvent received! TestId={TestId}, ContextType={ContextType}, Message={Message}, PublishedUtc={PublishedUtc}",
            evt.TestId,
            evt.ContextType,
            evt.Message,
            evt.PublishedUtc);

        return Task.CompletedTask;
    }
}
