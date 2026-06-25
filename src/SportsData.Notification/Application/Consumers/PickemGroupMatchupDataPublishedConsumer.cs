using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Upserts the local <see cref="PickemGroupMatchup"/> projection from a
    /// backfill snapshot. Race-safe insert via the unique
    /// <c>(PickemGroupId, ContestId)</c> index; change detection avoids
    /// flipping <see cref="PickemGroupMatchup.ModifiedUtc"/> on no-op
    /// redeliveries — same pattern as
    /// <see cref="UserDataPublishedConsumer"/> and
    /// <see cref="PickemGroupDataPublishedConsumer"/>.
    ///
    /// <para>
    /// <see cref="PickemGroupMatchup.StatusTypeName"/> is initialized to
    /// <c>"STATUS_SCHEDULED"</c> on insert and NEVER overwritten here. Status
    /// is owned by a future <c>ContestStatusChanged</c> consumer; this
    /// backfill consumer would clobber live state if it touched status.
    /// </para>
    /// </summary>
    public class PickemGroupMatchupDataPublishedConsumer : IConsumer<PickemGroupMatchupDataPublished>
    {
        private const string DefaultStatusTypeName = "STATUS_SCHEDULED";

        private readonly ILogger<PickemGroupMatchupDataPublishedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public PickemGroupMatchupDataPublishedConsumer(
            ILogger<PickemGroupMatchupDataPublishedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<PickemGroupMatchupDataPublished> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["PickemGroupId"] = msg.PickemGroupId,
                ["ContestId"] = msg.ContestId
            });

            var now = _dateTimeProvider.UtcNow();

            var existing = await _dataContext.PickemGroupMatchups
                .FirstOrDefaultAsync(
                    m => m.PickemGroupId == msg.PickemGroupId && m.ContestId == msg.ContestId,
                    context.CancellationToken);

            if (existing is null)
            {
                var entity = new PickemGroupMatchup
                {
                    PickemGroupId = msg.PickemGroupId,
                    ContestId = msg.ContestId,
                    StartDateUtc = msg.StartDateUtc,
                    // Seed event's CreatedUtc establishes the version
                    // baseline. Subsequent ContestStartTimeUpdated events
                    // with older CreatedUtc are rejected — they were
                    // already reflected in API's data at the moment the
                    // backfill responder published this seed.
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
                    _logger.LogInformation("PickemGroupMatchup projection inserted.");
                    return;
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    existing = await _dataContext.PickemGroupMatchups
                        .FirstAsync(
                            m => m.PickemGroupId == msg.PickemGroupId && m.ContestId == msg.ContestId,
                            context.CancellationToken);
                }
            }

            // Update path — touch Modified* only when a business field
            // actually differs. StatusTypeName is NOT considered: it's
            // owned by the (future) ContestStatusChanged consumer; this
            // backfill data event would clobber live state if we let it
            // overwrite.
            var changed =
                existing.StartDateUtc != msg.StartDateUtc
                || existing.SeasonYear != (msg.SeasonYear ?? 0)
                || existing.SeasonWeek != msg.SeasonWeek;

            if (!changed)
            {
                _logger.LogDebug("PickemGroupMatchup projection unchanged.");
                return;
            }

            // Refresh StartDateUpdatedAt only when StartDateUtc actually
            // shifted — keeps the version stable when other fields churn.
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
            _logger.LogInformation("PickemGroupMatchup projection updated.");
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
