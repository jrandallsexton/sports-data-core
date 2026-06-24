using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Headline v1 consumer: a user's pick was scored, send the result push.
    /// Every member who picked a finalized contest gets one of these events.
    ///
    /// <para>
    /// Orchestration: resolve prefs → resolve devices → dispatch via
    /// <see cref="IPushNotificationSender"/> → append audit row reflecting
    /// the outcome.
    /// </para>
    ///
    /// <para>
    /// Idempotency: NotificationLog has a unique <c>(CorrelationId, UserId, Channel)</c>
    /// index. MassTransit at-least-once delivery means we may consume the
    /// same event twice; the second attempt hits the dedupe path and
    /// returns without writing a second row or firing a second push.
    /// </para>
    /// </summary>
    public class UserPickScoredConsumer : IConsumer<UserPickScored>
    {
        private readonly ILogger<UserPickScoredConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushNotificationSender _pushSender;

        public UserPickScoredConsumer(
            ILogger<UserPickScoredConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushNotificationSender pushSender)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _pushSender = pushSender;
        }

        public async Task Consume(ConsumeContext<UserPickScored> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["UserId"] = msg.UserId,
                ["ContestId"] = msg.ContestId,
                ["LeagueId"] = msg.LeagueId,
                ["Sport"] = msg.Sport
            });

            _logger.LogInformation("UserPickScored received.");

            // Idempotency check: have we already attempted a Fcm send for this
            // correlation + user? If so, append a Skipped_Duplicate row and bail.
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
                    "UserPickScored already dispatched for CorrelationId {CorrelationId}, UserId {UserId}; skipping duplicate.",
                    msg.CorrelationId, msg.UserId);
                return;
            }

            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == msg.UserId);

            if (prefs is { PickResultEnabled: false })
            {
                await LogAndSaveAsync(msg, "Suppressed_UserOptedOut");
                return;
            }

            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == msg.UserId && d.NotificationsEnabled)
                .ToListAsync();

            if (devices.Count == 0)
            {
                await LogAndSaveAsync(msg, "Suppressed_NoDevice");
                return;
            }

            // Body composition needs the team-name fields off the event —
            // until the fat-payload pass lands, fall back to generic copy.
            var title = msg.IsCorrect == true ? "Nice pick!" : "Tough loss";
            var body = ComposeBody(msg);

            // Per-device dispatch. Single-token send rather than multicast
            // because v1's IPushNotificationSender surface is one-at-a-time;
            // multicast batching can come later if device counts per user
            // grow past a handful. Aggregate outcome:
            //   - any success → "Sent"
            //   - all failures → "Failed_FcmError" with reasons captured
            // Mixed outcomes still log "Sent" because the user did get the
            // push; per-device failure reasons stay in the structured logs.
            var successCount = 0;
            var failureReasons = new List<string>();
            foreach (var device in devices)
            {
                var result = await _pushSender.SendAsync(device.FcmToken, title, body);
                if (result is Success<string>)
                {
                    successCount++;
                }
                else if (result is Failure<string> failure)
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
                Category = "PickResult",
                Channel = "Fcm",
                Title = title,
                Body = body,
                Result = resultValue,
                FailureReason = failureReason,
                AttemptedUtc = _dateTimeProvider.UtcNow()
            });

            await _dataContext.SaveChangesAsync();
        }

        private static string ComposeBody(UserPickScored msg)
        {
            // Fat-payload fields not yet populated by the publisher fall back
            // to a generic line. Once AwayName/HomeName/PickValue land, swap
            // in: "{PickValue} {Won/Lost} — final {AwayName} {AwayScore} @ {HomeName} {HomeScore}"
            if (msg.AwayName is not null && msg.HomeName is not null)
            {
                return $"Final: {msg.AwayName} {msg.AwayScore} @ {msg.HomeName} {msg.HomeScore}";
            }

            return msg.IsCorrect == true
                ? $"Your pick won. Final {msg.AwayScore}–{msg.HomeScore}."
                : $"Your pick lost. Final {msg.AwayScore}–{msg.HomeScore}.";
        }

        private async Task LogAndSaveAsync(UserPickScored msg, string result)
        {
            _dataContext.NotificationLog.Add(new NotificationLog
            {
                UserId = msg.UserId,
                CorrelationId = msg.CorrelationId,
                Category = "PickResult",
                Channel = "Fcm",
                Result = result,
                AttemptedUtc = _dateTimeProvider.UtcNow()
            });
            await _dataContext.SaveChangesAsync();
        }
    }
}
