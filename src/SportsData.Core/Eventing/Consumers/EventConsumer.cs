using Microsoft.Extensions.Logging;
using SportsData.Core.Eventing.Consumers.Receivers;
using SportsData.Core.Eventing.Providers;
using SportsData.Core.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Consumers
{
    public class EventConsumer : IEventConsumer
    {

        private readonly ILogger<EventConsumer> _logger;
        private readonly IEventDataProvider _eventingData;
        private readonly IEventReceiver _eventReceiver;
        private readonly IEventHandlerProvider _eventHandlerProvider;

        public EventConsumer(ILogger<EventConsumer> logger,
            IEventDataProvider eventingData,
            IEventReceiver eventReceiver,
            IEventHandlerProvider eventHandlerProvider)
        {
            _logger = logger;
            _eventingData = eventingData;
            _eventReceiver = eventReceiver;
            _eventHandlerProvider = eventHandlerProvider;
        }

        public async Task ConsumeAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Begin consume");

            var eventHandlers = _eventHandlerProvider.GetEventHandlers();
            var incomingEvents = await _eventReceiver.Receive();

            if (incomingEvents == null || incomingEvents.Count == 0)
            {
                _logger?.LogInformation("No events to consume. Exiting.");
                return;
            }

            await incomingEvents.ForEachAsync(async e =>
            {
                _logger?.LogInformation("Consuming: {@e}", e);

                // convert to inbox
                var incomingEvent = Map(e);
                incomingEvent.LockedUtc = DateTime.UtcNow;
                await _eventingData.IncomingEvents.AddAsync(incomingEvent, cancellationToken);
                await _eventingData.SaveChangesAsync(cancellationToken);

                // remove event
                await _eventReceiver.Delete(e.Id);

                // look for event handler; invoke if found
                if (!eventHandlers.ContainsKey(e.EventType))
                {
                    _logger?.LogWarning("No event handlers found.");
                }
                else
                {
                    _logger?.LogInformation("Invoking handler.");
                    await eventHandlers[e.EventType](incomingEvent.EventPayload);
                    _logger?.LogInformation("Handler invoked.");
                }

                incomingEvent.LockedUtc = null;
                incomingEvent.Processed = true;
                incomingEvent.ProcessedUtc = DateTime.UtcNow;
                await _eventingData.SaveChangesAsync(cancellationToken);
            });
        }

        private static IncomingEvent Map(EventingBase sportsDataEvent)
        {
            // TODO: Rethink this
            return new IncomingEvent()
            {
                CausationId = sportsDataEvent.CausationId,
                CorrelationId = sportsDataEvent.CorrelationId,
                CreatedBy = sportsDataEvent.CreatedBy,
                CreatedUtc = sportsDataEvent.CreatedUtc,
                EventPayload = sportsDataEvent.EventPayload,
                EventType = sportsDataEvent.EventType,
                Id = sportsDataEvent.Id
            };
        }
    }
}
