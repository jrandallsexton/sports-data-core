using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Projects a user's per-category notification opt-in flags into the local
    /// <see cref="UserNotificationPreferences"/> table (the API is the canonical
    /// owner; it publishes <see cref="UserNotificationPreferencesUpdated"/> on
    /// every change). The dispatch consumers read this projection to gate sends;
    /// a user with no row is treated as all-enabled.
    ///
    /// <para>Idempotent by UserId — the event carries the full flag set, so this
    /// is a straight upsert. Race-safe insert: concurrent "doesn't exist" inserts
    /// let the unique-index loser (SQLSTATE 23505) fall through to the update
    /// path.</para>
    /// </summary>
    public class UserNotificationPreferencesUpdatedConsumer : IConsumer<UserNotificationPreferencesUpdated>
    {
        private readonly ILogger<UserNotificationPreferencesUpdatedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public UserNotificationPreferencesUpdatedConsumer(
            ILogger<UserNotificationPreferencesUpdatedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<UserNotificationPreferencesUpdated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["UserId"] = msg.UserId
            });

            var ct = context.CancellationToken;
            var now = _dateTimeProvider.UtcNow();

            var existing = await _dataContext.UserNotificationPreferences
                .FirstOrDefaultAsync(p => p.UserId == msg.UserId, ct);

            if (existing is null)
            {
                var entity = new UserNotificationPreferences
                {
                    UserId = msg.UserId,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                };
                Apply(entity, msg);
                _dataContext.UserNotificationPreferences.Add(entity);

                try
                {
                    await _dataContext.SaveChangesAsync(ct);
                    _logger.LogInformation("Notification preferences projection inserted. UserId={UserId}", msg.UserId);
                    return;
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Race: another consumer inserted first. Detach our orphan and
                    // fall through to the update path.
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    existing = await _dataContext.UserNotificationPreferences
                        .FirstAsync(p => p.UserId == msg.UserId, ct);
                }
            }

            Apply(existing, msg);
            existing.ModifiedUtc = now;
            existing.ModifiedBy = msg.CausationId;

            await _dataContext.SaveChangesAsync(ct);
            _logger.LogInformation("Notification preferences projection updated. UserId={UserId}", msg.UserId);
        }

        private static void Apply(UserNotificationPreferences entity, UserNotificationPreferencesUpdated msg)
        {
            entity.PickResultEnabled = msg.PickResultEnabled;
            entity.PickDeadlineReminderEnabled = msg.PickDeadlineReminderEnabled;
            entity.ContestStartReminderEnabled = msg.ContestStartReminderEnabled;
            entity.LeagueInviteEnabled = msg.LeagueInviteEnabled;
            entity.MembershipEnabled = msg.MembershipEnabled;
            entity.MatchupPreviewEnabled = msg.MatchupPreviewEnabled;
            entity.ScheduleChangeEnabled = msg.ScheduleChangeEnabled;
            entity.OddsChangedEnabled = msg.OddsChangedEnabled;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
