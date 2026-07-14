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
    /// Idempotency now rides on each reminder's typed table and its natural
    /// key — <c>NotificationPickDeadline (UserId, LeagueId, SeasonWeek,
    /// FireTimeUtc)</c> and <c>NotificationContestStart (UserId, ContestId,
    /// FireTimeUtc)</c>. The <c>FireTimeUtc</c> component is the version anchor:
    /// a Hangfire retry of the same fire collides and is suppressed, while a
    /// reschedule (new fire-time) is a new row and re-fires. The deterministic
    /// CorrelationId (stable MD5 over the parameters) is retained as a trace id
    /// for log correlation but no longer participates in dedup.
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

        public async Task SendPickDeadlineReminderAsync(Guid userId, Guid pickemGroupId, int seasonWeek, DateTime fireTimeUtc)
        {
            // fireTimeUtc IS the version anchor — what the scheduler intended
            // this job to fire at. Used both for the deterministic dedupe
            // key (a reschedule produces a different key per fire-time, so
            // an orphan can't suppress the new fire) and for the stale-fire
            // check below (an orphan reads the PendingScheduledJob row, sees
            // the scheduler has since moved to a different ScheduledFireUtc,
            // and aborts before sending the wrong-time push).
            var correlationId = DeterministicCorrelationId(
                "PickDeadline", userId, pickemGroupId, seasonWeek, fireTimeUtc.Ticks);

            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userId,
                ["PickemGroupId"] = pickemGroupId,
                ["SeasonWeek"] = seasonWeek,
                ["FireTimeUtc"] = fireTimeUtc
            });

            _logger.LogInformation("SendPickDeadlineReminderAsync invoked.");

            // Atomic claim on (UserId, LeagueId, SeasonWeek, FireTimeUtc) — the
            // natural key now does what the deterministic CorrelationId did
            // against NotificationLog: a Hangfire retry of the same fire collides
            // (suppressed) while a reschedule (new FireTimeUtc) re-fires.
            // See UserPickScoredConsumer for the claim-first rationale.
            var claim = new NotificationPickDeadline
            {
                UserId = userId,
                LeagueId = pickemGroupId,
                SeasonWeek = seasonWeek,
                FireTimeUtc = fireTimeUtc,
                CorrelationId = correlationId,
                Channel = "Fcm",
                Result = "Dispatching",
                AttemptedUtc = _dateTimeProvider.UtcNow()
            };
            _dataContext.NotificationPickDeadlines.Add(claim);

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

            // Stale-fire check: am I still the scheduler's intended fire?
            // Look up the PendingScheduledJob row by natural key. If the row
            // is gone, or its ScheduledFireUtc no longer matches what I was
            // called with, an orphan reschedule survived a failed best-
            // effort TryDelete and I'm the wrong-time fire. Abort.
            if (await IsStaleFireAsync(userId, "PickDeadline", pickemGroupId, seasonWeek, fireTimeUtc))
            {
                await FinalizeAsync(claim, "Suppressed_StaleFire");
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
                        // Dead token → prune the device; flushed by the terminal SaveChanges.
                        _dataContext.MarkDeadDeviceForRemoval(result, device.Id, _logger);
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

        public async Task SendContestStartReminderAsync(Guid userId, Guid contestId, DateTime fireTimeUtc)
        {
            // fireTimeUtc IS the version anchor — what the scheduler intended
            // this job to fire at. Used both for the deterministic dedupe
            // key (a reschedule produces a different key per fire-time) and
            // for the stale-fire check below (an orphan reads the
            // PendingScheduledJob row, sees the scheduler has since moved
            // to a different ScheduledFireUtc, and aborts before sending
            // the wrong-time push).
            var correlationId = DeterministicCorrelationId(
                "ContestStart", userId, contestId, fireTimeUtc.Ticks);

            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId,
                ["UserId"] = userId,
                ["ContestId"] = contestId,
                ["FireTimeUtc"] = fireTimeUtc
            });

            _logger.LogInformation("SendContestStartReminderAsync invoked.");

            // Atomic claim on (UserId, ContestId, FireTimeUtc) — natural-key dedup
            // with fire-time versioning, same as PickDeadline.
            var claim = new NotificationContestStart
            {
                UserId = userId,
                ContestId = contestId,
                FireTimeUtc = fireTimeUtc,
                CorrelationId = correlationId,
                Channel = "Fcm",
                Result = "Dispatching",
                AttemptedUtc = _dateTimeProvider.UtcNow()
            };
            _dataContext.NotificationContestStarts.Add(claim);

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

            // Stale-fire check: am I still the scheduler's intended fire?
            // ContestStart rows leave SeasonWeek null, included in the
            // natural key.
            if (await IsStaleFireAsync(userId, "ContestStart", contestId, seasonWeek: null, fireTimeUtc))
            {
                await FinalizeAsync(claim, "Suppressed_StaleFire");
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
                        // Dead token → prune the device; flushed by the terminal SaveChanges.
                        _dataContext.MarkDeadDeviceForRemoval(result, device.Id, _logger);
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

        // Terminal-state writes. Two overloads because the reminder tables are
        // independent (no shared base) — each dispatch method finalizes its own
        // typed claim.
        private async Task FinalizeAsync(NotificationPickDeadline claim, string result)
        {
            claim.Result = result;
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();
            await _dataContext.SaveChangesAsync();
        }

        private async Task FinalizeAsync(NotificationContestStart claim, string result)
        {
            claim.Result = result;
            claim.ModifiedUtc = _dateTimeProvider.UtcNow();
            await _dataContext.SaveChangesAsync();
        }

        /// <summary>
        /// Returns true when the currently-firing Hangfire job is no longer
        /// the scheduler's intended fire for this scope — either the row is
        /// gone (cancelled) or the scheduler has since rescheduled to a
        /// different fire-time. Callers should treat this as a no-op,
        /// finalize the claim as <c>Suppressed_StaleFire</c>, and return.
        ///
        /// <para>
        /// Lookup hits the natural-key unique index
        /// (UserId, JobKind, TargetId, SeasonWeek) — cheap, one indexed read
        /// per dispatch.
        /// </para>
        /// </summary>
        private async Task<bool> IsStaleFireAsync(
            Guid userId, string jobKind, Guid targetId, int? seasonWeek, DateTime fireTimeUtc)
        {
            var row = await _dataContext.PendingScheduledJobs
                .AsNoTracking()
                .FirstOrDefaultAsync(j =>
                    j.UserId == userId &&
                    j.JobKind == jobKind &&
                    j.TargetId == targetId &&
                    j.SeasonWeek == seasonWeek);

            if (row is null)
            {
                _logger.LogInformation(
                    "Stale fire: no PendingScheduledJob row for ({JobKind}, {TargetId}, week={SeasonWeek}); aborting.",
                    jobKind, targetId, seasonWeek);
                return true;
            }

            if (row.ScheduledFireUtc != fireTimeUtc)
            {
                _logger.LogInformation(
                    "Stale fire: scheduler has moved to FireTime={CurrentFireTime}; this job was scheduled for {OrphanFireTime}. Aborting.",
                    row.ScheduledFireUtc, fireTimeUtc);
                return true;
            }

            return false;
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
        /// MD5 over a canonical parameter encoding — a stable trace id, not
        /// cryptographic. Two calls with the same inputs produce the same Guid,
        /// giving a deterministic CorrelationId for log correlation across a
        /// reminder's retries. Dedup itself is handled by the typed table's
        /// natural key, not this value.
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
