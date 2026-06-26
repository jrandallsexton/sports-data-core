using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;
using SportsData.Notification.Infrastructure.Notifications;

namespace SportsData.Notification.Application.Dispatching
{
    /// <summary>
    /// Default <see cref="INotificationDispatcher"/>. Implements the
    /// atomic-claim + dispatch pattern shared with the event-driven consumers
    /// (<see cref="Consumers.UserPickScoredConsumer"/> etc.).
    ///
    /// <para>
    /// Deterministic CorrelationId: each method derives a stable Guid from
    /// its parameters via MD5(input bytes). Hangfire retrying the same call
    /// produces the same CorrelationId and collides on the
    /// <c>NotificationLog (CorrelationId, UserId, Channel)</c> unique
    /// constraint — Postgres rejects the second insert, the consumer falls
    /// through to the "already claimed" branch, and no duplicate push goes
    /// out. MD5 is fine here: this is a dedupe key, not a cryptographic
    /// signature.
    /// </para>
    /// </summary>
    public class NotificationDispatcher : INotificationDispatcher
    {
        private const int FailureReasonMaxLength = 512;
        private const string FailureReasonTruncationSuffix = "…(truncated)";

        private readonly ILogger<NotificationDispatcher> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPushNotificationSender _pushSender;

        public NotificationDispatcher(
            ILogger<NotificationDispatcher> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPushNotificationSender pushSender)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _pushSender = pushSender;
        }

        public async Task SendPickDeadlineReminderAsync(Guid userId, Guid pickemGroupId, int seasonWeek, DateTime deadlineUtc)
        {
            // Qualifiers include deadlineUtc.Ticks as the version anchor for
            // the same reason as ContestStart: a deadline shift (new matchup
            // lands and pulls MIN(StartDateUtc) earlier, or
            // ContestStartTimeUpdated moves the earliest game) reschedules
            // the Hangfire job. If the old job's TryDelete fails and it
            // fires first, the orphan would otherwise claim the
            // NotificationLog slot and silently suppress the new
            // correct-deadline reminder.
            var correlationId = DeterministicCorrelationId(
                "PickDeadline", userId, pickemGroupId, seasonWeek, deadlineUtc.Ticks);

            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userId,
                ["PickemGroupId"] = pickemGroupId,
                ["SeasonWeek"] = seasonWeek,
                ["DeadlineUtc"] = deadlineUtc
            });

            _logger.LogInformation("SendPickDeadlineReminderAsync invoked.");

            // Atomic claim — see UserPickScoredConsumer for the full
            // rationale (TOCTOU race, crash-vs-duplicate trade-off).
            var claim = new NotificationLog
            {
                UserId = userId,
                CorrelationId = correlationId,
                Category = "PickDeadline",
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
                    "PickDeadline reminder already dispatched for CorrelationId {CorrelationId}; skipping (Hangfire retry).",
                    correlationId);
                _dataContext.Entry(claim).State = EntityState.Detached;
                return;
            }

            // Prefs gate.
            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (prefs is { PickDeadlineReminderEnabled: false })
            {
                await FinalizeAsync(claim, "Suppressed_UserOptedOut");
                return;
            }

            // Device gate.
            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.NotificationsEnabled)
                .ToListAsync();

            if (devices.Count == 0)
            {
                await FinalizeAsync(claim, "Suppressed_NoDevice");
                return;
            }

            // Body composition — league name from the local projection.
            var leagueName = await _dataContext.PickemGroups
                .AsNoTracking()
                .Where(g => g.Id == pickemGroupId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync();

            const string title = "Picks due soon";
            var body = leagueName is not null
                ? $"Your picks for {leagueName} (Week {seasonWeek}) are due in about an hour."
                : $"Your picks for Week {seasonWeek} are due in about an hour.";

            // Per-device dispatch. Aggregate outcome same as the other
            // consumers (any success → "Sent"; all failures → "Failed_FcmError").
            //
            // Per-device try/catch: an unhandled exception from SendAsync
            // (e.g. network failure, TaskCanceledException, anything outside
            // FirebaseMessagingException which FirebasePushNotificationSender
            // already maps to Failure<string>) would otherwise escape before
            // claim finalization. The row would sit at "Dispatching"
            // permanently — Hangfire retries collide on the unique-constraint
            // dedupe path and short-circuit. Catching here lets one failing
            // device fail loudly in the audit log without blocking the
            // remaining devices or the terminal save.
            var successCount = 0;
            var failureReasons = new List<string>();
            foreach (var device in devices)
            {
                try
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Unexpected exception sending FCM to DeviceId {DeviceId}.",
                        device.Id);
                    failureReasons.Add($"{device.Platform}:exception:{ex.GetType().Name}");
                }
            }

            claim.Title = title;
            claim.Body = body;
            claim.Result = successCount > 0 ? "Sent" : "Failed_FcmError";
            claim.FailureReason = ComposeFailureReason(failureReasons);
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();

            await _dataContext.SaveChangesAsync();
        }

        public async Task SendContestStartReminderAsync(Guid userId, Guid contestId, DateTime startDateUtc)
        {
            // Qualifier is StartDateUtc.Ticks — the version anchor. Without
            // it, a rescheduled contest (ESPN moves start time, scheduler
            // creates a new Hangfire job, TryDelete of the old job fails)
            // would have the orphan job fire FIRST with the same
            // (category, userId, contestId) key, claim the NotificationLog
            // row, and silently suppress the correct-time fire. Including
            // Ticks gives each fire-time its own dedupe key so the new
            // reminder can land even when the orphan races it. Hangfire
            // retries of the SAME logical fire still dedupe correctly
            // because the serialized DateTime arg is stable across retries.
            var correlationId = DeterministicCorrelationId(
                "ContestStart", userId, contestId, startDateUtc.Ticks);

            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userId,
                ["ContestId"] = contestId,
                ["StartDateUtc"] = startDateUtc
            });

            _logger.LogInformation("SendContestStartReminderAsync invoked.");

            var claim = new NotificationLog
            {
                UserId = userId,
                CorrelationId = correlationId,
                Category = "ContestStart",
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
                    "ContestStart reminder already dispatched for CorrelationId {CorrelationId}; skipping (Hangfire retry).",
                    correlationId);
                _dataContext.Entry(claim).State = EntityState.Detached;
                return;
            }

            var prefs = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (prefs is { ContestStartReminderEnabled: false })
            {
                await FinalizeAsync(claim, "Suppressed_UserOptedOut");
                return;
            }

            var devices = await _dataContext.UserDevices
                .AsNoTracking()
                .Where(d => d.UserId == userId && d.NotificationsEnabled)
                .ToListAsync();

            if (devices.Count == 0)
            {
                await FinalizeAsync(claim, "Suppressed_NoDevice");
                return;
            }

            // Resolve sport for the user-facing copy. A Contest is sport-
            // specific, so every PickemGroup containing it shares one Sport
            // value — any matchup row's PickemGroup is authoritative.
            // Default to Sport.All when none found (defensive: sport lookup
            // races membership deletion); the terminology helper falls back
            // to generic "Game starting soon" copy in that case.
            var sport = await _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.ContestId == contestId)
                .Join(_dataContext.PickemGroups,
                    m => m.PickemGroupId,
                    g => g.Id,
                    (m, g) => g.Sport)
                .FirstOrDefaultAsync();

            var (title, body) = SportTerminology.GetContestStartCopy(sport);

            var successCount = 0;
            var failureReasons = new List<string>();
            foreach (var device in devices)
            {
                try
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
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Unexpected exception sending FCM to DeviceId {DeviceId}.",
                        device.Id);
                    failureReasons.Add($"{device.Platform}:exception:{ex.GetType().Name}");
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

        private static string ComposeFailureReason(List<string> failureReasons)
        {
            if (failureReasons.Count == 0)
                return null;

            var joined = string.Join("; ", failureReasons);
            if (joined.Length <= FailureReasonMaxLength)
                return joined;

            var cutoff = FailureReasonMaxLength - FailureReasonTruncationSuffix.Length;
            return joined.Substring(0, cutoff) + FailureReasonTruncationSuffix;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }

        /// <summary>
        /// MD5 over a canonical parameter encoding. Used only as a dedupe key —
        /// not cryptographic. Two calls with the same inputs produce the same
        /// Guid, which makes the NotificationLog unique constraint catch
        /// Hangfire retries.
        /// </summary>
        private static Guid DeterministicCorrelationId(string category, Guid userId, Guid scopeId, long qualifier)
        {
            // Qualifier is long so callers can pass a DateTime.Ticks version
            // anchor (ContestStart).
            var input = $"{category}|{userId:N}|{scopeId:N}|{qualifier}";
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hash);
        }

        private static Guid DeterministicCorrelationId(string category, Guid userId, Guid scopeId, long q1, long q2)
        {
            // Two-qualifier overload for callers that need both a scope
            // discriminator (e.g. PickDeadline's seasonWeek) AND a fire-time
            // version anchor (deadline Ticks) in the same key. Keeping
            // seasonWeek in the input means two different weeks of the same
            // league with the same deadline (rare but theoretically
            // possible across year boundaries) still hash distinctly.
            var input = $"{category}|{userId:N}|{scopeId:N}|{q1}|{q2}";
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hash);
        }
    }
}
