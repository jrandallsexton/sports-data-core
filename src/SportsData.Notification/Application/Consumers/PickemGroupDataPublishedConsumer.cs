using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Upserts the local <see cref="PickemGroup"/> projection from a backfill
    /// snapshot. Member rows use replace-semantics: existing memberships for
    /// the group are deleted and the event's bundled member list is inserted
    /// fresh. That keeps the local roster a faithful mirror of API's
    /// source-of-truth — including handling removed memberships that the
    /// upstream might no longer carry.
    /// </summary>
    public class PickemGroupDataPublishedConsumer : IConsumer<PickemGroupDataPublished>
    {
        private readonly ILogger<PickemGroupDataPublishedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public PickemGroupDataPublishedConsumer(
            ILogger<PickemGroupDataPublishedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupDataPublished> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["GroupId"] = msg.GroupId,
                ["Sport"] = msg.Sport
            });

            var now = _dateTimeProvider.UtcNow();

            // Upsert the group row.
            var group = await _dataContext.PickemGroups
                .FirstOrDefaultAsync(g => g.Id == msg.GroupId, context.CancellationToken);

            if (group is null)
            {
                _dataContext.PickemGroups.Add(new PickemGroup
                {
                    Id = msg.GroupId,
                    Name = msg.Name,
                    Sport = msg.Sport,
                    CommissionerUserId = msg.CommissionerUserId,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                });
                _logger.LogInformation("PickemGroup projection inserted. GroupId={GroupId}", msg.GroupId);
            }
            else
            {
                group.Name = msg.Name;
                group.Sport = msg.Sport;
                group.CommissionerUserId = msg.CommissionerUserId;
                group.ModifiedUtc = now;
                group.ModifiedBy = msg.CausationId;
                _logger.LogInformation("PickemGroup projection updated. GroupId={GroupId}", msg.GroupId);
            }

            // Replace members: delete existing rows for this group, then
            // insert the snapshot's set. Keeps the local roster a faithful
            // mirror including removals upstream.
            var existingMembers = await _dataContext.PickemGroupMembers
                .Where(m => m.PickemGroupId == msg.GroupId)
                .ToListAsync(context.CancellationToken);
            _dataContext.PickemGroupMembers.RemoveRange(existingMembers);

            foreach (var snapshot in msg.Members)
            {
                _dataContext.PickemGroupMembers.Add(new PickemGroupMember
                {
                    PickemGroupId = msg.GroupId,
                    UserId = snapshot.UserId,
                    Role = snapshot.Role,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                });
            }

            await _dataContext.SaveChangesAsync(context.CancellationToken);

            _logger.LogInformation(
                "PickemGroup roster synced. GroupId={GroupId}, MemberCount={MemberCount}",
                msg.GroupId, msg.Members.Count);
        }
    }
}
