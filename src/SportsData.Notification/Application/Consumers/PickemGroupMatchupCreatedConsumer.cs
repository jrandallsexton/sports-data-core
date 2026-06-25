using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Steady-state: a new matchup landed in a league. Upserts the local
    /// <see cref="PickemGroupMatchup"/> projection so subsequent reminder
    /// scheduling work has the data it needs.
    ///
    /// <para>
    /// Shares the projection contract with
    /// <see cref="PickemGroupMatchupDataPublishedConsumer"/> (the backfill
    /// path). Either consumer can run first depending on operator timing,
    /// so both use the same race-safe insert + change-detect pattern. The
    /// unique <c>(PickemGroupId, ContestId)</c> index makes the race tractable.
    /// </para>
    ///
    /// <para>
    /// Reminder scheduling itself (Hangfire job per league member at
    /// <c>StartDateUtc - leadTime</c>) is Phase 2c-main. This consumer is
    /// scoped to projection upkeep only — the reminder consumer (or this
    /// one expanded later) will read from the projection and schedule.
    /// </para>
    /// </summary>
    public class PickemGroupMatchupCreatedConsumer : IConsumer<PickemGroupMatchupCreated>
    {
        private const string DefaultStatusTypeName = "STATUS_SCHEDULED";

        private readonly ILogger<PickemGroupMatchupCreatedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public PickemGroupMatchupCreatedConsumer(
            ILogger<PickemGroupMatchupCreatedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupMatchupCreated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["GroupId"] = msg.GroupId,
                ["ContestId"] = msg.ContestId,
                ["Sport"] = msg.Sport
            });

            var now = _dateTimeProvider.UtcNow();

            var existing = await _dataContext.PickemGroupMatchups
                .FirstOrDefaultAsync(
                    m => m.PickemGroupId == msg.GroupId && m.ContestId == msg.ContestId,
                    context.CancellationToken);

            if (existing is null)
            {
                var entity = new PickemGroupMatchup
                {
                    PickemGroupId = msg.GroupId,
                    ContestId = msg.ContestId,
                    StartDateUtc = msg.StartDateUtc,
                    // See PickemGroupMatchupDataPublishedConsumer for the
                    // rationale on stamping StartDateUpdatedAt on insert.
                    StartDateUpdatedAt = msg.CreatedUtc,
                    SeasonYear = msg.SeasonYear ?? 0,
                    SeasonWeek = msg.SeasonWeek,
                    StatusTypeName = DefaultStatusTypeName,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                };
                _dataContext.PickemGroupMatchups.Add(entity);

                try
                {
                    await _dataContext.SaveChangesAsync(context.CancellationToken);
                    _logger.LogInformation("PickemGroupMatchup projection inserted from creation event.");
                    return;
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Race with the backfill consumer (or another redelivery)
                    // — peer write won, our orphan detaches, fall through to
                    // update.
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    existing = await _dataContext.PickemGroupMatchups
                        .FirstAsync(
                            m => m.PickemGroupId == msg.GroupId && m.ContestId == msg.ContestId,
                            context.CancellationToken);
                }
            }

            // Update path. StatusTypeName intentionally untouched — owned by
            // future ContestStatusChanged consumer, same rationale as the
            // backfill consumer.
            var changed =
                existing.StartDateUtc != msg.StartDateUtc
                || existing.SeasonYear != (msg.SeasonYear ?? 0)
                || existing.SeasonWeek != msg.SeasonWeek;

            if (!changed)
            {
                _logger.LogDebug("PickemGroupMatchup projection unchanged on creation redelivery.");
                return;
            }

            // Refresh StartDateUpdatedAt only when StartDateUtc actually
            // shifted — matches the seed consumer's discipline.
            var startDateChanged = existing.StartDateUtc != msg.StartDateUtc;

            existing.StartDateUtc = msg.StartDateUtc;
            if (startDateChanged)
            {
                existing.StartDateUpdatedAt = msg.CreatedUtc;
            }
            existing.SeasonYear = msg.SeasonYear ?? 0;
            existing.SeasonWeek = msg.SeasonWeek;
            existing.ModifiedUtc = now;
            existing.ModifiedBy = msg.CausationId;

            await _dataContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("PickemGroupMatchup projection updated from creation event.");
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
