using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Notification.Application.Dispatching;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Scheduling
{
    public interface IKickoffReminderScheduler
    {
        Task EvaluateAndScheduleForContestAsync(Guid contestId, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Decides whether to schedule (or reschedule, or no-op) a kickoff-soon
    /// reminder for every user who has the given contest in at least one of
    /// their leagues. Trigger points mirror the PickDeadline scheduler:
    /// <list type="bullet">
    ///   <item><c>PickemGroupMatchupCreatedConsumer</c> after upserting a new
    ///   matchup from the steady-state event.</item>
    ///   <item><c>PickemGroupMatchupDataPublishedConsumer</c> after upserting
    ///   a matchup from the operator-triggered backfill.</item>
    ///   <item><c>ContestStartTimeUpdatedConsumer</c> after resyncing
    ///   <c>StartDateUtc</c> for matchups referencing the changed contest.</item>
    /// </list>
    ///
    /// <para>
    /// Scope is per-contest, not per-league-week: a user in three leagues that
    /// each contain the same contest gets ONE kickoff reminder. The unique
    /// index on (UserId, JobKind, TargetId, SeasonWeek=null) collapses
    /// duplicates across leagues for free — the second insert hits 23505 and
    /// falls through to the reschedule branch against the winner's row.
    /// </para>
    ///
    /// <para>
    /// Lead time is 30 minutes, hardcoded for v1 (vs PickDeadline's 1 hour) —
    /// kickoff is a "live-game prep" nudge, not a "pick your games" reminder.
    /// </para>
    /// </summary>
    public class KickoffReminderScheduler : IKickoffReminderScheduler
    {
        // Hardcoded for v1 — per the design discussion. Will become a
        // per-user pref later when we surface notification timing controls.
        private static readonly TimeSpan KickoffLeadTime = TimeSpan.FromMinutes(30);

        private const string JobKind = "Kickoff";

        private readonly ILogger<KickoffReminderScheduler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public KickoffReminderScheduler(
            ILogger<KickoffReminderScheduler> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task EvaluateAndScheduleForContestAsync(
            Guid contestId,
            CancellationToken cancellationToken)
        {
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["ContestId"] = contestId
            });

            // StartDateUtc is identical across every league's projection row
            // for this contest (ContestStartTimeUpdated bulk-updates them all),
            // so any row is fine. Use Min to be defensive about transient skew.
            var startDate = await _dataContext.PickemGroupMatchups
                .Where(m => m.ContestId == contestId)
                .Select(m => (DateTime?)m.StartDateUtc)
                .MinAsync(cancellationToken);

            if (startDate is null)
            {
                _logger.LogDebug("No matchup projection rows for contest; nothing to schedule.");
                return;
            }

            var fireTime = startDate.Value - KickoffLeadTime;
            var now = _dateTimeProvider.UtcNow();

            if (fireTime <= now)
            {
                // Kickoff is past or within the 30-minute window. A "starts
                // in 30 minutes" reminder fired 5 minutes pre-kickoff would
                // be misleading; skip rather than send-immediate.
                _logger.LogInformation(
                    "Kickoff {StartDate} is past or within lead window from now {Now}; skipping schedule.",
                    startDate, now);
                return;
            }

            // Distinct member ids across every league that contains this
            // contest. Same user in three leagues = one fan-out target —
            // the unique index collapses duplicates, but pre-deduping
            // avoids 2 wasted Hangfire schedule+catch cycles per extra
            // membership.
            var memberIds = await _dataContext.PickemGroupMatchups
                .Where(m => m.ContestId == contestId)
                .Join(_dataContext.PickemGroupMembers,
                    matchup => matchup.PickemGroupId,
                    member => member.PickemGroupId,
                    (matchup, member) => member.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);

            if (memberIds.Count == 0)
            {
                _logger.LogDebug("No members across all leagues containing this contest; nothing to schedule.");
                return;
            }

            _logger.LogInformation(
                "Evaluating Kickoff reminders for {MemberCount} distinct members. StartDate={StartDate}, FireTime={FireTime}",
                memberIds.Count, startDate, fireTime);

            // Existing kickoff jobs for this contest, across all users.
            var existingByUser = await _dataContext.PendingScheduledJobs
                .Where(j => j.JobKind == JobKind && j.TargetId == contestId)
                .ToDictionaryAsync(j => j.UserId, cancellationToken);

            var optedOutUserIds = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .Where(p => memberIds.Contains(p.UserId) && !p.KickoffReminderEnabled)
                .Select(p => p.UserId)
                .ToListAsync(cancellationToken);
            var optedOut = optedOutUserIds.ToHashSet();

            foreach (var userId in memberIds)
            {
                if (optedOut.Contains(userId))
                {
                    continue;
                }

                existingByUser.TryGetValue(userId, out var existing);
                await ScheduleOrRescheduleAsync(userId, contestId, fireTime, existing, now, cancellationToken);
            }
        }

        private async Task ScheduleOrRescheduleAsync(
            Guid userId,
            Guid contestId,
            DateTime fireTime,
            PendingScheduledJob existing,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (existing is not null && existing.ScheduledFireUtc == fireTime)
            {
                // Kickoff hasn't moved — no work.
                return;
            }

            // Schedule the new job FIRST, then persist, then delete the old.
            // Same crash-safe ordering as PickDeadlineReminderScheduler.
            var delay = fireTime - now;
            var newJobId = _backgroundJobProvider.Schedule<INotificationDispatcher>(
                d => d.SendKickoffReminderAsync(userId, contestId),
                delay);

            if (existing is null)
            {
                var entity = new PendingScheduledJob
                {
                    UserId = userId,
                    JobKind = JobKind,
                    TargetId = contestId,
                    SeasonWeek = null,
                    HangfireJobId = newJobId,
                    ScheduledFireUtc = fireTime,
                    CreatedUtc = now,
                    CreatedBy = Guid.Empty
                };
                _dataContext.PendingScheduledJobs.Add(entity);

                try
                {
                    await _dataContext.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
                {
                    // Peer-takeover branch — identical shape to PickDeadline.
                    // Common cause for Kickoff: same contest is in N leagues
                    // and we already scheduled this user from a sibling
                    // matchup row.
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    var winner = await _dataContext.PendingScheduledJobs
                        .FirstAsync(j => j.UserId == userId
                                         && j.JobKind == JobKind
                                         && j.TargetId == contestId
                                         && j.SeasonWeek == null,
                                    cancellationToken);

                    if (winner.ScheduledFireUtc == fireTime)
                    {
                        TryDeleteHangfireJob(newJobId);
                        return;
                    }

                    var winnerOldJobId = winner.HangfireJobId;
                    winner.HangfireJobId = newJobId;
                    winner.ScheduledFireUtc = fireTime;
                    winner.ModifiedUtc = now;
                    await _dataContext.SaveChangesAsync(cancellationToken);
                    TryDeleteHangfireJob(winnerOldJobId);
                    _logger.LogInformation(
                        "Concurrent insert resolved; took over scheduling. UserId={UserId}, FireTime={FireTime}",
                        userId, fireTime);
                    return;
                }
            }
            else
            {
                var oldJobId = existing.HangfireJobId;
                existing.HangfireJobId = newJobId;
                existing.ScheduledFireUtc = fireTime;
                existing.ModifiedUtc = now;
                await _dataContext.SaveChangesAsync(cancellationToken);

                TryDeleteHangfireJob(oldJobId);
            }

            _logger.LogInformation(
                "Scheduled Kickoff reminder. UserId={UserId}, FireTime={FireTime}, HangfireJobId={HangfireJobId}",
                userId, fireTime, newJobId);
        }

        private void TryDeleteHangfireJob(string jobId)
        {
            try
            {
                _backgroundJobProvider.Delete(jobId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to delete Hangfire job {JobId}. Absorbed by dispatcher dedupe.",
                    jobId);
            }
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
