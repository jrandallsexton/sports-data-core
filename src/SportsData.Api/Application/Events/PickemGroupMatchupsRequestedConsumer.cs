using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Backfill responder for the Notification service's local matchup
    /// projection. Iterates <c>PickemGroupMatchup</c> filtered to future
    /// games only (<c>StartDateUtc &gt; UtcNow</c>) and emits one
    /// <see cref="PickemGroupMatchupDataPublished"/> per row.
    ///
    /// <para>
    /// Read-only on the API side; publishes via
    /// <see cref="IMessageDeliveryScope"/> Direct mode + PublishBatch — same
    /// pattern as <c>UsersRequestedConsumer</c> /
    /// <c>PickemGroupsRequestedConsumer</c>. PublishBatch bypasses
    /// EventBus.Publish's per-call 1-second delay (the "give consumers time
    /// to commit" hack), which would be fatal for a wide fan-out.
    /// </para>
    ///
    /// <para>
    /// Past matchups are intentionally excluded — picks have locked, no
    /// reminder is meaningful, and republishing the entire history would
    /// flood Rabbit with events for events.
    /// </para>
    /// </summary>
    public class PickemGroupMatchupsRequestedConsumer : IConsumer<PickemGroupMatchupsRequested>
    {
        private readonly ILogger<PickemGroupMatchupsRequestedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IEventBus _eventBus;
        private readonly IMessageDeliveryScope _deliveryScope;
        private readonly IDateTimeProvider _dateTimeProvider;

        public PickemGroupMatchupsRequestedConsumer(
            ILogger<PickemGroupMatchupsRequestedConsumer> logger,
            AppDataContext dataContext,
            IEventBus eventBus,
            IMessageDeliveryScope deliveryScope,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _eventBus = eventBus;
            _deliveryScope = deliveryScope;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupMatchupsRequested> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["Sport"] = msg.Sport
            });

            var now = _dateTimeProvider.UtcNow();

            // PickemGroupMatchup carries Sport via its parent PickemGroup —
            // join through to filter so Sport.All requests fan-out cleanly
            // and per-sport requests don't return foreign data.
            var query = _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Join(_dataContext.PickemGroups,
                      m => m.GroupId,
                      g => g.Id,
                      (m, g) => new { Matchup = m, g.Sport })
                .Where(x => x.Matchup.StartDateUtc > now);

            if (msg.Sport != Sport.All)
            {
                query = query.Where(x => x.Sport == msg.Sport);
            }

            var matchups = await query
                .Select(x => new
                {
                    x.Matchup.GroupId,
                    x.Matchup.ContestId,
                    x.Matchup.StartDateUtc,
                    x.Matchup.SeasonYear,
                    x.Matchup.SeasonWeek,
                    x.Sport
                })
                .ToListAsync(context.CancellationToken);

            _logger.LogInformation(
                "PickemGroupMatchupsRequested received; publishing PickemGroupMatchupDataPublished for {Count} future matchups.",
                matchups.Count);

            var events = matchups
                .Select(m => new PickemGroupMatchupDataPublished(
                    m.GroupId,
                    m.ContestId,
                    m.StartDateUtc,
                    m.SeasonWeek,
                    m.Sport,
                    m.SeasonYear,
                    msg.CorrelationId,
                    Guid.NewGuid()))
                .ToList();

            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                await _eventBus.PublishBatch(events, context.CancellationToken);
            }

            _logger.LogInformation(
                "PickemGroupMatchupsRequested backfill complete; published {Count} PickemGroupMatchupDataPublished events.",
                matchups.Count);
        }
    }
}
