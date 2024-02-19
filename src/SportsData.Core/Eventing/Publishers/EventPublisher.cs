using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SportsData.Core.Eventing.Providers;
using SportsData.Core.Eventing.Publishers.Broadcasters;
using SportsData.Core.Extensions;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace SportsData.Core.Eventing.Publishers
{
    public class EventPublisher : IEventPublisher
    {
        private readonly ILogger<EventPublisher> _logger;
        private readonly IEventDataProvider _eventingData;
        private readonly IEventBroadcaster _eventBroadcaster;

        public EventPublisher(ILogger<EventPublisher> logger,
            IEventDataProvider eventingData,
            IEventBroadcaster eventBroadcaster)
        {
            _logger = logger;
            _eventingData = eventingData;
            _eventBroadcaster = eventBroadcaster;
        }

        public async Task PublishAsync(CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Begin publish");

            var events = await _eventingData.OutgoingEvents
                .Where(e => e.Raised == false && !e.LockedUtc.HasValue)
                .ToListAsync(cancellationToken);

            if (events.Count == 0)
            {
                _logger?.LogInformation("Nothing to publish. Existing.");
                return;
            }

            _logger?.LogInformation($"Publishing {events.Count} events.");

            await events.ForEachAsync(async e =>
            {
                e.LockedUtc = DateTime.UtcNow;
                await _eventingData.SaveChangesAsync(cancellationToken);
            });

            await events.ForEachAsync(async e =>
            {
                await _eventBroadcaster.Broadcast(e);
                e.Raised = true;
                e.RaisedUtc = DateTime.UtcNow;
                e.LockedUtc = null;
                await _eventingData.SaveChangesAsync(cancellationToken);
            });
        }
    }
}
