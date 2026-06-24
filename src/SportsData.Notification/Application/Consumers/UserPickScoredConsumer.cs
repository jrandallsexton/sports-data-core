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
    /// Orchestration: <b>atomic-claim</b> via NotificationLog insert
    /// (relies on the unique <c>(CorrelationId, UserId, Channel)</c> index)
    /// → resolve prefs → resolve devices → dispatch via
    /// <see cref="IPushNotificationSender"/> → update the row with terminal outcome.
    /// </para>
    ///
    /// <para>
    /// The claim-first pattern closes a TOCTOU race the prior AnyAsync-then-insert
    /// flow had: two concurrent redeliveries could both read "no row" and both
    /// dispatch before either inserted. Now the first INSERT wins the unique
    /// constraint; the loser gets <see cref="DbUpdateException"/> and returns
    /// without dispatching.
    /// </para>
    ///
    /// <para>
    /// Failure mode: if the consumer crashes between claim and terminal update,
    /// the row sits at <c>Result="Dispatching"</c> indefinitely and any
    /// redelivery is suppressed by the unique constraint. We've chosen a
    /// missing notification over a duplicate notification for v1 — duplicates
    /// are worse UX than absences. A future cleanup job could rerun stale
    /// Dispatching rows if needed.
    /// </para>
    /// </summary>
    public class UserPickScoredConsumer : IConsumer<UserPickScored>
    {
        // NotificationLog.FailureReason is varchar(512). Truncate the joined
        // per-device summary so we don't trip Postgres's length check on a
        // user with many failing devices; per-device detail stays in logs.
        private const int FailureReasonMaxLength = 512;
        private const string FailureReasonTruncationSuffix = "…(truncated)";

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

            // Atomic claim. Insert a NotificationLog row in Dispatching state
            // BEFORE doing any work. If another consumer beat us to it, the
            // unique constraint throws DbUpdateException and we bail without
            // dispatching anything.
            var claim = new NotificationLog
            {
                UserId = msg.UserId,
                CorrelationId = msg.CorrelationId,
                Category = "PickResult",
                Channel = "Fcm",
                Result = "Dispatching",
                AttemptedUtc = _dateTimeProvider.UtcNow()
            };
            _dataContext.NotificationLog.Add(claim);

            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                _logger.LogInformation(
                    "UserPickScored already claimed by another consumer for CorrelationId {CorrelationId}, UserId {UserId}; skipping.",
                    msg.CorrelationId, msg.UserId);
                _dataContext.Entry(claim).State = EntityState.Detached;
                return;
            }

            // From here on we own the dispatch. Any terminal state writes
            // UPDATE the claim row rather than insert a new one.
            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == msg.UserId);

            if (prefs is { PickResultEnabled: false })
            {
                await FinalizeAsync(claim, "Suppressed_UserOptedOut");
                return;
            }

            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == msg.UserId && d.NotificationsEnabled)
                .ToListAsync();

            if (devices.Count == 0)
            {
                await FinalizeAsync(claim, "Suppressed_NoDevice");
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

            claim.Title = title;
            claim.Body = body;
            claim.Result = successCount > 0 ? "Sent" : "Failed_FcmError";
            claim.FailureReason = ComposeFailureReason(failureReasons);
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();

            await _dataContext.SaveChangesAsync();
        }

        private async Task FinalizeAsync(NotificationLog claim, string result)
        {
            claim.Result = result;
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();
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

        private static string ComposeFailureReason(List<string> failureReasons)
        {
            if (failureReasons.Count == 0)
                return null;

            var joined = string.Join("; ", failureReasons);
            if (joined.Length <= FailureReasonMaxLength)
                return joined;

            // Reserve room for the suffix; full per-device detail stays in
            // structured logs via FirebasePushNotificationSender.
            var cutoff = FailureReasonMaxLength - FailureReasonTruncationSuffix.Length;
            return joined.Substring(0, cutoff) + FailureReasonTruncationSuffix;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505. Inspecting
            // SqlState on the inner PostgresException is the load-bearing
            // check; the EF-side DbUpdateException is too generic on its own.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
