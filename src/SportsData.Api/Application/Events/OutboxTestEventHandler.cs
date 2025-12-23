using MassTransit;
using SportsData.Core.Eventing.Events;

namespace SportsData.Api.Application.Events;

/// <summary>
/// Handler for OutboxTestEvent in API service.
/// This validates that API receives events published by Producer.
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
            "? [API] OutboxTestEvent received! TestId={TestId}, ContextType={ContextType}, Message={Message}, PublishedUtc={PublishedUtc}",
            evt.TestId,
            evt.ContextType,
            evt.Message,
            evt.PublishedUtc);

        return Task.CompletedTask;
    }
}
