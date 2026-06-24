using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.PickemGroups;

namespace SportsData.Api.Application.Events
{
    /// <summary>
    /// Backfill responder for PickemGroups. Iterates the API's PickemGroups
    /// table and emits one <see cref="PickemGroupDataPublished"/> per league
    /// with the member roster bundled into the payload. Bundle (vs separate
    /// per-member events) keeps the projection insert atomic on the
    /// Notification side and avoids the parent-before-child race.
    ///
    /// <para>
    /// Read-only; uses <see cref="IMessageDeliveryScope"/> Direct mode to
    /// bypass the outbox — same pattern as
    /// <see cref="UsersRequestedConsumer"/> and
    /// <see cref="PickemGroups.PickemGroupCreatedHandler"/>'s sibling flows.
    /// </para>
    /// </summary>
    public class PickemGroupsRequestedConsumer : IConsumer<PickemGroupsRequested>
    {
        private readonly ILogger<PickemGroupsRequestedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IEventBus _eventBus;
        private readonly IMessageDeliveryScope _deliveryScope;

        public PickemGroupsRequestedConsumer(
            ILogger<PickemGroupsRequestedConsumer> logger,
            AppDataContext dataContext,
            IEventBus eventBus,
            IMessageDeliveryScope deliveryScope)
        {
            _logger = logger;
            _dataContext = dataContext;
            _eventBus = eventBus;
            _deliveryScope = deliveryScope;
        }

        public async Task Consume(ConsumeContext<PickemGroupsRequested> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["Sport"] = msg.Sport
            });

            // Single round-trip to load groups + members. AsSplitQuery avoids
            // the cartesian explosion that .Include() would cause on a wide
            // membership table.
            var groups = await _dataContext.PickemGroups
                .AsNoTracking()
                .Include(g => g.Members)
                .AsSplitQuery()
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.Sport,
                    g.CommissionerUserId,
                    Members = g.Members
                        .Select(m => new { m.UserId, Role = m.Role.ToString() })
                        .ToList()
                })
                .ToListAsync(context.CancellationToken);

            _logger.LogInformation(
                "PickemGroupsRequested received; publishing PickemGroupDataPublished for {Count} groups.",
                groups.Count);

            // Batch via PublishBatch to dodge EventBus.Publish's per-call
            // 1-second Direct-mode delay (EventBus.cs:102). At dozens of
            // leagues that's already meaningful sleep time; per-call is
            // strictly worse than batch. See sibling notes in
            // UsersRequestedConsumer for the trade-off (no header-side
            // X-Correlation-Id stamp; event body still carries it).
            var events = groups
                .Select(group => new PickemGroupDataPublished(
                    group.Id,
                    group.Name,
                    group.CommissionerUserId,
                    group.Members
                        .Select(m => new PickemGroupMemberSnapshot(m.UserId, m.Role))
                        .ToList(),
                    group.Sport,
                    msg.SeasonYear,
                    msg.CorrelationId,
                    Guid.NewGuid()))
                .ToList();

            using (_deliveryScope.Use(DeliveryMode.Direct))
            {
                await _eventBus.PublishBatch(events, context.CancellationToken);
            }

            _logger.LogInformation(
                "PickemGroupsRequested backfill complete; published {Count} PickemGroupDataPublished events.",
                groups.Count);
        }
    }
}
