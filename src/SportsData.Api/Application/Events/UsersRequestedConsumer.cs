using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Users;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Backfill responder. The Notification service (or any future consumer
    /// needing to seed its local User projection) publishes
    /// <see cref="UsersRequested"/>; this handler iterates the API's Users
    /// table and emits one <see cref="UserDataPublished"/> per user.
    ///
    /// <para>
    /// Read-only on the API side — no DbContext writes, so publishes go
    /// through <see cref="IMessageDeliveryScope"/> in Direct mode to bypass
    /// the bus-outbox (consistent with PickemGroupWeekMatchupsGeneratedHandler
    /// and the existing direct-publish pattern in this project).
    /// </para>
    ///
    /// <para>
    /// Synthetic users are intentionally INCLUDED — Notification's local
    /// projection is a faithful mirror of who's on file; filtering belongs
    /// in the dispatch path (don't notify synthetic users), not here.
    /// Repeated invocations republish the full set; consumer-side upsert
    /// makes that safe.
    /// </para>
    /// </summary>
    public class UsersRequestedConsumer : IConsumer<UsersRequested>
    {
        private readonly ILogger<UsersRequestedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IEventBus _eventBus;
        private readonly IMessageDeliveryScope _deliveryScope;

        public UsersRequestedConsumer(
            ILogger<UsersRequestedConsumer> logger,
            AppDataContext dataContext,
            IEventBus eventBus,
            IMessageDeliveryScope deliveryScope)
        {
            _logger = logger;
            _dataContext = dataContext;
            _eventBus = eventBus;
            _deliveryScope = deliveryScope;
        }

        public async Task Consume(ConsumeContext<UsersRequested> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["Sport"] = msg.Sport
            });

            var users = await _dataContext.Users
                .AsNoTracking()
                .Select(u => new { u.Id, u.DisplayName, u.Email, u.Timezone })
                .ToListAsync(context.CancellationToken);

            _logger.LogInformation(
                "UsersRequested received; publishing UserDataPublished for {Count} users.",
                users.Count);

            // Batch via PublishBatch rather than foreach _eventBus.Publish.
            // EventBus.Publish in Direct mode pays a 1-second Task.Delay per
            // call (the "give consumers time to commit" hack at EventBus.cs:102)
            // — fatal for batch fan-out: N users would mean N seconds of
            // sleep. PublishBatch calls _bus.Publish directly in 256-chunk
            // Task.WhenAll batches, bypassing the delay. Trade-off: PublishBatch
            // doesn't auto-stamp the X-Correlation-Id header, but every event
            // body already carries CorrelationId as a positional record field;
            // consumers read from the body.
            var events = users
                .Select(user => new UserDataPublished(
                    user.Id,
                    user.DisplayName,
                    user.Email,
                    user.Timezone ?? string.Empty,
                    msg.Sport,
                    msg.SeasonYear,
                    msg.CorrelationId,
                    Guid.NewGuid()))
                .ToList();

            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                await _eventBus.PublishBatch(events, context.CancellationToken);
            }

            _logger.LogInformation(
                "UsersRequested backfill complete; published {Count} UserDataPublished events.",
                users.Count);
        }
    }
}
