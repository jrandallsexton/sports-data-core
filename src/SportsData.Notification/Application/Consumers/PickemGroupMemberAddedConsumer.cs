using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.PickemGroups;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// A user joined a league. Drives the "#5 welcome + commissioner-side
    /// notification" pair from the catalog in
    /// <c>docs/architecture/notification-service-events-and-state.md</c> §2:
    ///
    /// <list type="bullet">
    ///   <item>Welcome push to the joining user (gated on
    ///   <see cref="UserNotificationPreferences.MembershipEnabled"/>).</item>
    ///   <item>"New member joined" push to the commissioner (same gate, same
    ///   category — both are membership-category notifications).</item>
    /// </list>
    ///
    /// <para>
    /// <b>Known fat-payload gap:</b> the current event carries
    /// <c>GroupId, UserId, Sport, SeasonYear</c> only. To send useful copy we
    /// also need <c>LeagueName</c>, <c>JoinedDisplayName</c>, and
    /// <c>CommissionerUserId</c>. See design doc §4 — the canonical name for
    /// this fat event is <c>PickemGroupMembershipCreated</c>; today's
    /// <c>PickemGroupMemberAdded</c> is the existing record kept for
    /// compatibility, but the fattening pass should align names too.
    /// </para>
    /// </summary>
    public class PickemGroupMemberAddedConsumer : IConsumer<PickemGroupMemberAdded>
    {
        private readonly ILogger<PickemGroupMemberAddedConsumer> _logger;
        private readonly AppDataContext _dataContext;

        public PickemGroupMemberAddedConsumer(
            ILogger<PickemGroupMemberAddedConsumer> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task Consume(ConsumeContext<PickemGroupMemberAdded> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["GroupId"] = msg.GroupId,
                ["UserId"] = msg.UserId,
                ["Sport"] = msg.Sport
            });

            _logger.LogInformation("PickemGroupMemberAdded received.");

            // 1. Resolve the joining user's preferences + device(s).
            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == msg.UserId);

            if (prefs is { MembershipEnabled: false })
            {
                _logger.LogInformation(
                    "Membership-category notifications disabled for UserId {UserId}; suppressing welcome push.",
                    msg.UserId);
                // Still write a log row so suppression is auditable.
                await LogSuppressedAsync(msg, "Suppressed_UserOptedOut");
                return;
            }

            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == msg.UserId && d.NotificationsEnabled)
                .ToListAsync();

            if (devices.Count == 0)
            {
                _logger.LogInformation(
                    "No active UserDevice rows for UserId {UserId}; suppressing welcome push.",
                    msg.UserId);
                await LogSuppressedAsync(msg, "Suppressed_NoDevice");
                return;
            }

            // 2. Send welcome push.
            // TODO (FCM integration): inject IPushNotificationSender (or a
            // Notification-local equivalent) and dispatch the "Welcome to
            // {LeagueName}" push for each device. The sender owns FCM call,
            // retry, and per-token failure mapping. This consumer just owns
            // the orchestration + audit row.
            foreach (var device in devices)
            {
                _logger.LogInformation(
                    "TODO: send welcome FCM to DeviceId {DeviceId} for UserId {UserId}.",
                    device.Id, msg.UserId);
            }

            // 3. Notify commissioner of new member.
            // TODO (fat-payload work): the commissioner's UserId isn't on the
            // event today — fattening required. Once available, run the same
            // preference + device lookup against the commissioner and dispatch
            // a "{JoinedDisplayName} joined {LeagueName}" push.

            // 4. Append audit row.
            _dataContext.NotificationLog.Add(new NotificationLog
            {
                UserId = msg.UserId,
                CorrelationId = msg.CorrelationId,
                Category = "Membership",
                Channel = "Fcm",
                Title = "Welcome",
                Body = "TODO: render with LeagueName once fattened",
                Result = "Sent",
                AttemptedUtc = DateTime.UtcNow
            });

            await _dataContext.SaveChangesAsync();
        }

        private async Task LogSuppressedAsync(PickemGroupMemberAdded msg, string result)
        {
            _dataContext.NotificationLog.Add(new NotificationLog
            {
                UserId = msg.UserId,
                CorrelationId = msg.CorrelationId,
                Category = "Membership",
                Channel = "Fcm",
                Result = result,
                AttemptedUtc = DateTime.UtcNow
            });
            await _dataContext.SaveChangesAsync();
        }
    }
}
