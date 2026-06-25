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
        // Bounded page size so memory stays flat regardless of dataset size.
        // Each page allocates ~PageSize event objects; PublishBatch will
        // further chunk to 256 internally for Task.WhenAll fan-out.
        private const int PageSize = 500;

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

            // Honor SeasonYear when supplied so a future controller variant
            // can scope a backfill (e.g., "just 2026"). Today the controller
            // hardcodes null, which falls through here.
            if (msg.SeasonYear is int seasonYear)
            {
                query = query.Where(x => x.Matchup.SeasonYear == seasonYear);
            }

            // OrderBy is purely so Skip/Take has deterministic results within
            // a single page query — it does NOT guarantee stable paging
            // across concurrent inserts/deletes (PK is a Guid, not a
            // monotonic sequence; new rows can land at any position in the
            // ordering). Duplicate or skipped rows are possible if the
            // table mutates mid-backfill. Acceptable here because the
            // downstream consumer is idempotent (race-safe upsert with
            // change-detect), so a row seen twice is a no-op and a row
            // missed will be picked up by the next backfill or by the
            // steady-state PickemGroupMatchupCreated event.
            var pagedQuery = query
                .OrderBy(x => x.Matchup.Id)
                .Select(x => new
                {
                    x.Matchup.GroupId,
                    x.Matchup.ContestId,
                    x.Matchup.StartDateUtc,
                    x.Matchup.SeasonYear,
                    x.Matchup.SeasonWeek,
                    x.Sport
                });

            _logger.LogInformation(
                "PickemGroupMatchupsRequested received; paging through future matchups in chunks of {PageSize}.",
                PageSize);

            // Hold the Direct scope across pages — same AsyncLocal value
            // applies for the lifetime of the using block.
            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                var totalPublished = 0;
                var offset = 0;

                while (true)
                {
                    var page = await pagedQuery
                        .Skip(offset)
                        .Take(PageSize)
                        .ToListAsync(context.CancellationToken);

                    if (page.Count == 0)
                        break;

                    var events = page
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

                    await _eventBus.PublishBatch(events, context.CancellationToken);

                    totalPublished += events.Count;
                    offset += PageSize;

                    _logger.LogDebug(
                        "Published page of {Count} events; running total {Total}.",
                        events.Count, totalPublished);

                    // Last page detection — short-circuits one extra empty
                    // query at the end. Safe under concurrent inserts: a new
                    // matchup landing mid-backfill gets a higher Id and just
                    // appears on a later page, never displaces a row we've
                    // already paged.
                    if (page.Count < PageSize)
                        break;
                }

                _logger.LogInformation(
                    "PickemGroupMatchupsRequested backfill complete; published {Count} PickemGroupMatchupDataPublished events.",
                    totalPublished);
            }
        }
    }
}
