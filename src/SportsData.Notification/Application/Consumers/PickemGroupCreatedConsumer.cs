using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Projects a newly created league into the local <see cref="PickemGroup"/>
    /// table at creation time, so the projection is queryable immediately rather
    /// than only after an operator backfill (<see cref="PickemGroupsRequested"/>).
    /// This is what makes line-move targeting correct for brand-new leagues:
    /// <see cref="ContestOddsUpdatedConsumer"/> reads <c>PickType</c> from this
    /// row to decide whether a league's pickers care about the odds.
    ///
    /// <para>
    /// Group-level fields only — members are NOT seeded here. The commissioner
    /// (and any other members) arrive via <c>PickemGroupMemberAdded</c> and the
    /// backfill's <c>PickemGroupDataPublished</c>; this consumer deliberately
    /// leaves the roster alone so it can't race the member-sync path.
    /// </para>
    ///
    /// <para>
    /// Idempotent upsert keyed on the PK: race-safe insert (loser of the
    /// unique-constraint insert falls through to the update path), and a
    /// redelivery that finds the row unchanged is a no-op.
    /// </para>
    /// </summary>
    public class PickemGroupCreatedConsumer : IConsumer<PickemGroupCreated>
    {
        private readonly ILogger<PickemGroupCreatedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public PickemGroupCreatedConsumer(
            ILogger<PickemGroupCreatedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupCreated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["GroupId"] = msg.GroupId,
                ["Sport"] = msg.Sport
            });

            var now = _dateTimeProvider.UtcNow();

            // Backward-compat: a message published before PickType existed (in
            // flight during rollout) deserializes with null. Treat null as the
            // safe odds-agnostic default so we never write null into the
            // required column or over-notify on a line move.
            var pickType = msg.PickType ?? LeaguePickType.StraightUp;

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
                    PickType = pickType,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                });

                try
                {
                    await _dataContext.SaveChangesAsync(context.CancellationToken);
                    _logger.LogInformation("PickemGroup projection inserted on create. GroupId={GroupId}", msg.GroupId);
                    return;
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Race: the backfill's PickemGroupDataPublished (or a
                    // redelivery) inserted first. Re-read and fall through to
                    // the change-detection update path.
                    _dataContext.ChangeTracker.Clear();
                    group = await _dataContext.PickemGroups
                        .FirstAsync(g => g.Id == msg.GroupId, context.CancellationToken);
                }
            }

            var changed =
                group.Name != msg.Name
                || group.Sport != msg.Sport
                || group.CommissionerUserId != msg.CommissionerUserId
                || group.PickType != pickType;

            if (!changed)
            {
                _logger.LogDebug("PickemGroup projection already current on create. GroupId={GroupId}", msg.GroupId);
                return;
            }

            group.Name = msg.Name;
            group.Sport = msg.Sport;
            group.CommissionerUserId = msg.CommissionerUserId;
            group.PickType = pickType;
            group.ModifiedUtc = now;
            group.ModifiedBy = msg.CausationId;

            await _dataContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("PickemGroup projection updated on create. GroupId={GroupId}", msg.GroupId);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
