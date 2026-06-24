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
    /// snapshot. Member rows use replace-semantics: when the snapshot set
    /// differs from the local set, existing rows for the group are deleted
    /// and the snapshot's set is inserted fresh. Keeps the local roster a
    /// faithful mirror of API's source-of-truth — including removals.
    ///
    /// <para>
    /// Race-safe insert: two concurrent consumers seeing "doesn't exist"
    /// can both try; the PK-unique constraint catches the loser via
    /// <see cref="DbUpdateException"/> and we fall through to update.
    /// </para>
    ///
    /// <para>
    /// Churn-free redelivery: business fields are diffed before applying.
    /// If neither the group row nor the member set differs, no writes
    /// happen and <c>ModifiedUtc</c> stays put.
    /// </para>
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

            // ---- Group row: upsert with race-safe insert + change detection.
            var group = await _dataContext.PickemGroups
                .FirstOrDefaultAsync(g => g.Id == msg.GroupId, context.CancellationToken);

            var groupChanged = false;
            if (group is null)
            {
                var entity = new PickemGroup
                {
                    Id = msg.GroupId,
                    Name = msg.Name,
                    Sport = msg.Sport,
                    CommissionerUserId = msg.CommissionerUserId,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                };
                _dataContext.PickemGroups.Add(entity);

                try
                {
                    await _dataContext.SaveChangesAsync(context.CancellationToken);
                    _logger.LogInformation("PickemGroup projection inserted. GroupId={GroupId}", msg.GroupId);
                    group = entity;
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Race: another consumer inserted first. Detach our
                    // orphan and fall through to the update path.
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    group = await _dataContext.PickemGroups
                        .FirstAsync(g => g.Id == msg.GroupId, context.CancellationToken);
                    groupChanged = ApplyGroupUpdateIfChanged(group, msg, now);
                }
            }
            else
            {
                groupChanged = ApplyGroupUpdateIfChanged(group, msg, now);
            }

            // ---- Members: set-equality check before replace.
            var existingMembers = await _dataContext.PickemGroupMembers
                .Where(m => m.PickemGroupId == msg.GroupId)
                .ToListAsync(context.CancellationToken);

            var existingSet = existingMembers
                .Select(m => (m.UserId, m.Role))
                .ToHashSet();
            var snapshotSet = msg.Members
                .Select(m => (m.UserId, m.Role))
                .ToHashSet();

            var membersChanged = !existingSet.SetEquals(snapshotSet);

            if (membersChanged)
            {
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
            }

            if (groupChanged || membersChanged)
            {
                await _dataContext.SaveChangesAsync(context.CancellationToken);
                _logger.LogInformation(
                    "PickemGroup roster synced. GroupId={GroupId}, MemberCount={MemberCount}, GroupChanged={GroupChanged}, MembersChanged={MembersChanged}",
                    msg.GroupId, msg.Members.Count, groupChanged, membersChanged);
            }
            else
            {
                _logger.LogDebug("PickemGroup projection unchanged. GroupId={GroupId}", msg.GroupId);
            }
        }

        private static bool ApplyGroupUpdateIfChanged(PickemGroup group, PickemGroupDataPublished msg, DateTime now)
        {
            var changed =
                group.Name != msg.Name
                || group.Sport != msg.Sport
                || group.CommissionerUserId != msg.CommissionerUserId;

            if (!changed)
                return false;

            group.Name = msg.Name;
            group.Sport = msg.Sport;
            group.CommissionerUserId = msg.CommissionerUserId;
            group.ModifiedUtc = now;
            group.ModifiedBy = msg.CausationId;
            return true;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
