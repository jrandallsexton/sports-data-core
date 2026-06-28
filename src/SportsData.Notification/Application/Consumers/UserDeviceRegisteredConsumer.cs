using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Users;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Projects an FCM device registration from the API into the local
    /// <see cref="UserDevice"/> table — the store every reminder/result
    /// dispatch reads to find a user's tokens. Mirrors the
    /// <see cref="UserDataPublishedConsumer"/> projection pattern.
    ///
    /// <para>
    /// Idempotent on the <c>(UserId, FcmToken)</c> natural key (unique index):
    /// re-registration on every app launch / token refresh and at-least-once
    /// redelivery all converge on one row. Race-safe insert — a concurrent
    /// consumer losing the unique-constraint insert falls through to the
    /// update path.
    /// </para>
    ///
    /// <para>
    /// On re-registration we refresh <c>LastSeenUtc</c> and <c>Platform</c>
    /// but deliberately do NOT reset <see cref="UserDevice.NotificationsEnabled"/>
    /// — that's the user's per-device opt-out and a token refresh must not
    /// silently re-enable a device they turned off.
    /// </para>
    /// </summary>
    public class UserDeviceRegisteredConsumer : IConsumer<UserDeviceRegistered>
    {
        private readonly ILogger<UserDeviceRegisteredConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;

        public UserDeviceRegisteredConsumer(
            ILogger<UserDeviceRegisteredConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Consume(ConsumeContext<UserDeviceRegistered> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["UserId"] = msg.UserId,
                ["Platform"] = msg.Platform
            });

            var now = _dateTimeProvider.UtcNow();

            var existing = await _dataContext.UserDevices
                .FirstOrDefaultAsync(
                    d => d.UserId == msg.UserId && d.FcmToken == msg.FcmToken,
                    context.CancellationToken);

            if (existing is null)
            {
                var entity = new UserDevice
                {
                    Id = Guid.NewGuid(),
                    UserId = msg.UserId,
                    FcmToken = msg.FcmToken,
                    Platform = msg.Platform,
                    NotificationsEnabled = true,
                    LastSeenUtc = now,
                    CreatedUtc = now,
                    CreatedBy = msg.CausationId
                };
                _dataContext.UserDevices.Add(entity);

                try
                {
                    await _dataContext.SaveChangesAsync(context.CancellationToken);
                    _logger.LogInformation("UserDevice registered. UserId={UserId}, DeviceId={DeviceId}", msg.UserId, entity.Id);
                    return;
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Race: another consumer inserted the same (UserId, FcmToken)
                    // first. Detach our orphan and fall through to update.
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    existing = await _dataContext.UserDevices
                        .FirstAsync(
                            d => d.UserId == msg.UserId && d.FcmToken == msg.FcmToken,
                            context.CancellationToken);
                }
            }

            // Refresh liveness + platform; preserve the user's opt-out.
            existing.LastSeenUtc = now;
            existing.Platform = msg.Platform;
            existing.ModifiedUtc = now;
            existing.ModifiedBy = msg.CausationId;

            await _dataContext.SaveChangesAsync(context.CancellationToken);
            _logger.LogInformation("UserDevice refreshed. UserId={UserId}, DeviceId={DeviceId}", msg.UserId, existing.Id);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
