using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// A registered user was invited to a league. Push an invite whose tap
    /// deep-links to the league-invite preview (league name + Join CTA). The
    /// API only publishes this for an existing, non-member invitee, so there's
    /// no membership re-check here — just dispatch.
    ///
    /// <para>
    /// First notification to populate the FCM <c>data</c> payload. The mobile
    /// tap handler dispatches on <c>kind</c>; <c>leagueId</c> drives the
    /// preview + subsequent join. See
    /// <c>docs/mobile/league-invite-deep-link.md</c>.
    /// </para>
    ///
    /// <para>
    /// Single-recipient dispatch: atomic <see cref="NotificationLeagueInvitation"/>
    /// claim on the unique <c>(UserId, LeagueId, CorrelationId)</c> index
    /// (idempotent across redelivery of one invite; a re-invite re-notifies) →
    /// prefs → devices → send → terminal update.
    /// </para>
    /// </summary>
    public class UserInvitedToPickemGroupConsumer : IConsumer<UserInvitedToPickemGroup>
    {
        private readonly ILogger<UserInvitedToPickemGroupConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushNotificationSender _pushSender;

        public UserInvitedToPickemGroupConsumer(
            ILogger<UserInvitedToPickemGroupConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushNotificationSender pushSender)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _pushSender = pushSender;
        }

        public async Task Consume(ConsumeContext<UserInvitedToPickemGroup> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["GroupId"] = msg.GroupId,
                ["InviteeUserId"] = msg.InviteeUserId
            });

            _logger.LogInformation("UserInvitedToPickemGroup received.");

            // Atomic claim keyed on (UserId, LeagueId, CorrelationId): idempotent
            // across redelivery of the same invite, while a genuine re-invite
            // (new CorrelationId) re-notifies.
            var claim = new NotificationLeagueInvitation
            {
                UserId = msg.InviteeUserId,
                LeagueId = msg.GroupId,
                InvitedByUserId = msg.InvitedByUserId,
                CorrelationId = msg.CorrelationId,
                Channel = "Fcm",
                Result = "Dispatching",
                AttemptedUtc = _dateTimeProvider.UtcNow()
            };
            _dataContext.NotificationLeagueInvitations.Add(claim);

            try
            {
                await _dataContext.SaveChangesAsync(context.CancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                // Already claimed by a prior attempt. Skip unconditionally —
                // including a stale "Dispatching" row from a crash — for the same
                // deliberate v1 reason as the other consumers: a missing
                // notification beats a duplicate. Stale rows are a future cleanup
                // job, not recovered here.
                _logger.LogInformation(
                    "League-invite notification already claimed for UserId {UserId}, LeagueId {LeagueId} (CorrelationId {CorrelationId}); skipping.",
                    msg.InviteeUserId, msg.GroupId, msg.CorrelationId);
                _dataContext.Entry(claim).State = EntityState.Detached;
                return;
            }

            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == msg.InviteeUserId, context.CancellationToken);

            if (prefs is { LeagueInviteEnabled: false })
            {
                await FinalizeAsync(claim, "Suppressed_UserOptedOut", context.CancellationToken);
                return;
            }

            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == msg.InviteeUserId && d.NotificationsEnabled)
                .ToListAsync(context.CancellationToken);

            if (devices.Count == 0)
            {
                await FinalizeAsync(claim, "Suppressed_NoDevice", context.CancellationToken);
                return;
            }

            var title = "You're invited";
            var body = $"You've been invited to {msg.LeagueName}.";
            var data = new Dictionary<string, string>
            {
                ["kind"] = "LeagueInvite",
                ["target"] = "invite-preview",
                ["leagueId"] = msg.GroupId.ToString()
            };

            var successCount = 0;
            foreach (var device in devices)
            {
                var result = await _pushSender.SendAsync(
                    device.FcmToken, title, body, data, context.CancellationToken);
                if (result is Success<string>)
                    successCount++;
                else
                    // Dead token → prune the device (isolated best-effort save).
                    await _dataContext.MarkDeadDeviceForRemovalAsync(
                        result, device.Id, _logger, context.CancellationToken);
            }

            claim.Title = title;
            claim.Body = body;
            claim.Result = successCount > 0 ? "Sent" : "Failed_FcmError";
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();

            await _dataContext.SaveChangesAsync(context.CancellationToken);
        }

        private async Task FinalizeAsync(NotificationLeagueInvitation claim, string result, CancellationToken cancellationToken)
        {
            claim.Result = result;
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();
            await _dataContext.SaveChangesAsync(cancellationToken);
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
