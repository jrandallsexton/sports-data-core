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
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushNotificationSender _pushSender;

        public PickemGroupMemberAddedConsumer(
            ILogger<PickemGroupMemberAddedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushNotificationSender pushSender)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _pushSender = pushSender;
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

            // Idempotency: at-least-once delivery from RabbitMQ means we may
            // see the same event twice. Dedupe on (CorrelationId, UserId, Channel)
            // — same key the design doc §3 calls out for NotificationLog — and
            // write a Skipped_Duplicate audit row instead of firing a second
            // welcome push. Mirrors the guard in UserPickScoredConsumer.
            var alreadyAttempted = await _dataContext.NotificationLog
                .AsNoTracking()
                .AnyAsync(l =>
                    l.CorrelationId == msg.CorrelationId &&
                    l.UserId == msg.UserId &&
                    l.Channel == "Fcm");

            if (alreadyAttempted)
            {
                // The first delivery's NotificationLog row IS the audit
                // trail. Writing a second row here would collide with the
                // unique (CorrelationId, UserId, Channel) index and throw.
                // Log + return; redelivery is silently absorbed.
                _logger.LogInformation(
                    "PickemGroupMemberAdded already dispatched for CorrelationId {CorrelationId}, UserId {UserId}; skipping duplicate.",
                    msg.CorrelationId, msg.UserId);
                return;
            }

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

            // 2. Send welcome push. Body stays generic until the fat-payload
            // pass adds LeagueName + JoinedDisplayName to the event.
            const string title = "Welcome";
            const string body = "You've joined a new league.";

            // 3. Commissioner-side notification is deferred — the event
            // doesn't carry CommissionerUserId today. Once fattened, repeat
            // the prefs + device lookup against the commissioner here.

            var successCount = 0;
            var failureReasons = new List<string>();
            foreach (var device in devices)
            {
                var sendResult = await _pushSender.SendAsync(device.FcmToken, title, body);
                if (sendResult is Success<string>)
                {
                    successCount++;
                }
                else if (sendResult is Failure<string> failure)
                {
                    var reason = failure.Errors.FirstOrDefault()?.ErrorMessage ?? "unknown";
                    failureReasons.Add($"{device.Platform}:{reason}");
                }
            }

            var resultValue = successCount > 0 ? "Sent" : "Failed_FcmError";
            var failureReason = failureReasons.Count > 0
                ? string.Join("; ", failureReasons)
                : null;

            _dataContext.NotificationLog.Add(new NotificationLog
            {
                UserId = msg.UserId,
                CorrelationId = msg.CorrelationId,
                Category = "Membership",
                Channel = "Fcm",
                Title = title,
                Body = body,
                Result = resultValue,
                FailureReason = failureReason,
                AttemptedUtc = _dateTimeProvider.UtcNow()
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
                AttemptedUtc = _dateTimeProvider.UtcNow()
            });
            await _dataContext.SaveChangesAsync();
        }
    }
}
