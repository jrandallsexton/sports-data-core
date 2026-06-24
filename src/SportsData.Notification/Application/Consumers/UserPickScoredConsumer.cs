using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Eventing.Events.Picks;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// Headline v1 consumer: a user's pick was scored, send the result push.
    /// Every member who picked a finalized contest gets one of these events.
    ///
    /// <para>
    /// Orchestration is the same shape as <see cref="PickemGroupMemberAddedConsumer"/>:
    /// resolve prefs → resolve devices → suppress vs send → append audit row.
    /// FCM dispatch itself is delegated to a future <c>IPushNotificationSender</c>
    /// (today the only sender implementation lives in the API project; moving
    /// or duplicating it into Notification is part of the FCM-integration TODO).
    /// </para>
    ///
    /// <para>
    /// Idempotency: NotificationLog has a unique-ish index on
    /// <c>(CorrelationId, UserId, Channel)</c>. Even though MassTransit at-least-once
    /// delivery means we may consume the same event twice, the second attempt
    /// hits the dedupe path and writes <c>Result = "Skipped_Duplicate"</c>
    /// instead of firing a second push.
    /// </para>
    /// </summary>
    public class UserPickScoredConsumer : IConsumer<UserPickScored>
    {
        private readonly ILogger<UserPickScoredConsumer> _logger;
        private readonly AppDataContext _dataContext;

        public UserPickScoredConsumer(
            ILogger<UserPickScoredConsumer> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
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
                _logger.LogInformation(
                    "UserPickScored already dispatched for CorrelationId {CorrelationId}, UserId {UserId}; skipping duplicate.",
                    msg.CorrelationId, msg.UserId);

                _dataContext.NotificationLog.Add(new NotificationLog
                {
                    UserId = msg.UserId,
                    CorrelationId = msg.CorrelationId,
                    Category = "PickResult",
                    Channel = "Fcm",
                    Result = "Skipped_Duplicate",
                    AttemptedUtc = DateTime.UtcNow
                });
                await _dataContext.SaveChangesAsync();
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

            // TODO (FCM integration): inject IPushNotificationSender and dispatch
            // per device. Body composition needs the team-name fields off the
            // event — until the fat-payload pass lands, fall back to generic copy.
            var title = msg.IsCorrect == true ? "Nice pick!" : "Tough loss";
            var body = ComposeBody(msg);

            foreach (var device in devices)
            {
                _logger.LogInformation(
                    "TODO: send PickResult FCM to DeviceId {DeviceId} for UserId {UserId}. Title='{Title}', Body='{Body}'",
                    device.Id, msg.UserId, title, body);
            }

            _dataContext.NotificationLog.Add(new NotificationLog
            {
                UserId = msg.UserId,
                CorrelationId = msg.CorrelationId,
                Category = "PickResult",
                Channel = "Fcm",
                Title = title,
                Body = body,
                Result = "Sent",
                AttemptedUtc = DateTime.UtcNow
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
                AttemptedUtc = DateTime.UtcNow
            });
            await _dataContext.SaveChangesAsync();
        }
    }
}
