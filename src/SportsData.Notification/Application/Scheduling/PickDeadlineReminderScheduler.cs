using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Notification.Application.Reminders.Commands.SendPickDeadlineReminder;
using SportsData.Notification.Config;
using SportsData.Notification.Infrastructure.Data;
using SportsData.Notification.Infrastructure.Data.Entities;

namespace SportsData.Notification.Application.Scheduling
{
    public interface IPickDeadlineReminderScheduler
    {
        Task EvaluateAndScheduleForLeagueWeekAsync(Guid pickemGroupId, int seasonWeek, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Decides whether to schedule (or reschedule, or no-op) a pick-deadline
    /// reminder for every member of a given league-week. The trigger is
    /// "this league-week's matchup state may have changed" — fired by:
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
    /// v2 wave model (docs/features/pick-deadline-reminders-v2.md): picks
    /// lock PER GAME at kickoff, so the week's distinct kickoff times are
    /// clustered into waves — a kickoff joins the current wave when it is
    /// within <c>PickDeadlineCoalesceMinutes</c> of the wave's anchor
    /// (earliest kickoff), otherwise it starts a new wave. One reminder per
    /// (member, wave), firing at <c>anchor - PickDeadlineLeadMinutes</c>.
    /// Whether the member actually gets a push is decided at fire time by
    /// the dispatcher's missing-pick gate.
    /// </para>
    ///
    /// <para>
    /// Per (member, wave), the scheduler walks <c>PendingScheduledJob</c>
    /// entries for <c>(UserId, "PickDeadline", PickemGroupId, SeasonWeek,
    /// WaveAnchorUtc)</c> and:
    /// <list type="bullet">
    ///   <item>Inserts + schedules if no row exists and the fire time is
    ///   still in the future.</item>
    ///   <item>Reschedules (schedule-new → save → delete-old, same crash-safe
    ///   ordering as Producer's <c>CompetitionStreamScheduler</c>) when the
    ///   wave's fire time has moved.</item>
    ///   <item>No-ops when unchanged or already past.</item>
    ///   <item>Deletes future-scheduled rows unless some kickoff in their
    ///   window [anchor, anchor + coalesce] is UNCOVERED by every schedulable
    ///   re-derived wave — v1 rows (null anchor) always delete. This is the
    ///   balance point between two failure modes: anchor-set orphaning
    ///   silently dropped the sole remaining cover after an earlier-moving
    ///   kickoff merged waves, while any-kickoff-in-window retention
    ///   double-pushed after a later-moving kickoff re-anchored a wave a new
    ///   row already covers. In-flight rows (fire time reached) are left
    ///   alone so a legitimate mid-fire dispatch isn't stale-fired by its
    ///   own scheduler.</item>
    /// </list>
    /// </para>
    /// </summary>
    public class PickDeadlineReminderScheduler : IPickDeadlineReminderScheduler
    {
        private const string JobKind = "PickDeadline";

        private readonly ILogger<PickDeadlineReminderScheduler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly NotificationConfig _config;

        public PickDeadlineReminderScheduler(
            ILogger<PickDeadlineReminderScheduler> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IProvideBackgroundJobs backgroundJobProvider,
            IOptions<NotificationConfig> config)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _backgroundJobProvider = backgroundJobProvider;
            _config = config.Value;
        }

        /// <summary>
        /// Clusters sorted distinct kickoff times into waves: a kickoff joins
        /// the current wave when within <paramref name="coalesceWindow"/> of
        /// the wave's anchor (its earliest kickoff); otherwise it anchors a
        /// new wave. Members are returned alongside each anchor so orphan
        /// cleanup can reason about which kickoffs a wave covers.
        /// </summary>
        private static List<(DateTime Anchor, List<DateTime> Kickoffs)> DeriveWaves(
            IReadOnlyList<DateTime> sortedDistinctKickoffs, TimeSpan coalesceWindow)
        {
            var waves = new List<(DateTime Anchor, List<DateTime> Kickoffs)>();
            foreach (var kickoff in sortedDistinctKickoffs)
            {
                if (waves.Count == 0 || kickoff - waves[^1].Anchor > coalesceWindow)
                {
                    waves.Add((kickoff, new List<DateTime>()));
                }
                waves[^1].Kickoffs.Add(kickoff);
            }
            return waves;
        }

        public async Task EvaluateAndScheduleForLeagueWeekAsync(
            Guid pickemGroupId,
            int seasonWeek,
            CancellationToken cancellationToken)
        {
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["PickemGroupId"] = pickemGroupId,
                ["SeasonWeek"] = seasonWeek
            });

            // Derive the week's kickoff waves from current DB state. Empty =
            // no matchups for this league-week (e.g., matchups got deleted);
            // orphan cleanup below still cancels any future-scheduled rows.
            var kickoffs = await _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.PickemGroupId == pickemGroupId && m.SeasonWeek == seasonWeek)
                .Select(m => m.StartDateUtc)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync(cancellationToken);

            var lead = TimeSpan.FromMinutes(_config.PickDeadlineLeadMinutes);
            var coalesce = TimeSpan.FromMinutes(_config.PickDeadlineCoalesceMinutes);
            var waves = DeriveWaves(kickoffs, coalesce);
            var now = _dateTimeProvider.UtcNow();

            // Waves whose fire time is past (or is now) can't get a "soon"
            // reminder — skip scheduling but keep their rows (an in-flight
            // fire must not be stale-fired by its own scheduler).
            var schedulableAnchors = waves
                .Select(w => w.Anchor)
                .Where(a => a - lead > now)
                .ToList();

            // Kickoffs NOT covered by any schedulable re-derived wave — their
            // wave's fire time is already past, so only a surviving stale row
            // can still remind about them.
            var schedulableSet = schedulableAnchors.ToHashSet();
            var uncoveredKickoffs = waves
                .Where(w => !schedulableSet.Contains(w.Anchor))
                .SelectMany(w => w.Kickoffs)
                .ToList();

            // Existing rows for the whole league-week in one query — cheaper
            // than per-user round trips for a 30-person league.
            var existingRows = await _dataContext.PendingScheduledJobs
                .Where(j => j.JobKind == JobKind
                            && j.TargetId == pickemGroupId
                            && j.SeasonWeek == seasonWeek)
                .ToListAsync(cancellationToken);

            // Orphan cleanup: a future-scheduled row survives when its anchor
            // is itself a schedulable derived anchor (the current wave's own
            // row — ScheduleOrReschedule no-ops or reschedules it below), OR
            // while some kickoff in its window [anchor, anchor + coalesce] is
            // UNCOVERED (its re-derived wave can no longer fire). Rationale,
            // both directions:
            //   - Anchor-set membership alone over-deleted: a kickoff moving
            //     EARLIER merges waves, and when the merged wave's fire time
            //     is already past, the old row is the sole remaining cover
            //     for the unmoved games — deleting it silently dropped their
            //     reminder.
            //   - Window-contains-any-kickoff alone over-kept: a kickoff
            //     moving LATER within the coalesce window re-anchors the
            //     wave, a new schedulable row covers everything, and the
            //     stale row would fire a near-duplicate push minutes apart.
            // v1 rows (null anchor) are always orphans. Rows at/past their
            // fire time are left alone: the fire may be mid-dispatch, and
            // deleting the row would trip the dispatcher's stale-fire gate
            // on a legitimate send.
            var orphans = existingRows
                .Where(j => j.ScheduledFireUtc > now
                            && (j.WaveAnchorUtc is null
                                || (!schedulableSet.Contains(j.WaveAnchorUtc.Value)
                                    && !uncoveredKickoffs.Any(k => k >= j.WaveAnchorUtc.Value
                                                                   && k <= j.WaveAnchorUtc.Value + coalesce))))
                .ToList();
            if (orphans.Count > 0)
            {
                _dataContext.PendingScheduledJobs.RemoveRange(orphans);
                await _dataContext.SaveChangesAsync(cancellationToken);
                foreach (var orphan in orphans)
                {
                    TryDeleteHangfireJob(orphan.HangfireJobId);
                }
                _logger.LogInformation(
                    "Cancelled {Count} orphaned PickDeadline rows (anchors no longer derived).",
                    orphans.Count);
            }

            if (schedulableAnchors.Count == 0)
            {
                _logger.LogDebug("No schedulable kickoff waves for league-week; nothing to schedule.");
                return;
            }

            // Resolve members of the league. The local PickemGroupMember
            // projection is the source of truth.
            var memberIds = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Where(m => m.PickemGroupId == pickemGroupId)
                .Select(m => m.UserId)
                .ToListAsync(cancellationToken);

            if (memberIds.Count == 0)
            {
                _logger.LogDebug("No members for league; nothing to schedule.");
                return;
            }

            _logger.LogInformation(
                "Evaluating PickDeadline reminders for {MemberCount} members across {WaveCount} kickoff waves.",
                memberIds.Count, schedulableAnchors.Count);

            // Skip the per-user prefs lookup inside the loop by batching.
            var optedOutUserIds = await _dataContext.UserNotificationPreferences
                .AsNoTracking()
                .Where(p => memberIds.Contains(p.UserId) && !p.PickDeadlineReminderEnabled)
                .Select(p => p.UserId)
                .ToListAsync(cancellationToken);
            var optedOut = optedOutUserIds.ToHashSet();

            var existingByUserAndAnchor = existingRows
                .Where(j => j.WaveAnchorUtc is not null && !orphans.Contains(j))
                .ToDictionary(j => (j.UserId, j.WaveAnchorUtc.Value));

            foreach (var userId in memberIds)
            {
                if (optedOut.Contains(userId))
                {
                    // Honor opted-out users by NOT scheduling. If a stale row
                    // exists for them from a prior pass (they opted out
                    // after), leave it — the dispatcher's prefs gate will
                    // suppress the fire and audit it.
                    continue;
                }

                foreach (var anchor in schedulableAnchors)
                {
                    existingByUserAndAnchor.TryGetValue((userId, anchor), out var existing);
                    await ScheduleOrRescheduleAsync(
                        userId, pickemGroupId, seasonWeek, anchor, anchor - lead,
                        existing, now, cancellationToken);
                }
            }
        }

        private async Task ScheduleOrRescheduleAsync(
            Guid userId,
            Guid pickemGroupId,
            int seasonWeek,
            DateTime waveAnchorUtc,
            DateTime fireTime,
            PendingScheduledJob existing,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (existing is not null && existing.ScheduledFireUtc == fireTime)
            {
                // Wave's fire time hasn't moved — no work.
                return;
            }

            // Schedule the new job FIRST, then persist, then delete the old.
            // Crash-safe ordering: a crash between schedule and save leaks
            // an orphan Hangfire job (benign — dispatcher's dedupe absorbs
            // it). The alternative (delete-old first) could leave a row
            // pointing at a deleted job, silently missing the reminder.
            // fireTime is forwarded to the dispatcher as the version anchor.
            // It feeds both the deterministic CorrelationId (so a reschedule
            // gets a fresh dedupe key) AND the dispatcher's stale-fire check
            // against PendingScheduledJob.ScheduledFireUtc — an orphan that
            // survived a failed best-effort delete will see the row no
            // longer matches its fireTime and abort before sending.
            var delay = fireTime - now;
            var newJobId = _backgroundJobProvider.Schedule<ISendPickDeadlineReminderCommandHandler>(
                d => d.ExecuteAsync(userId, pickemGroupId, seasonWeek, fireTime, waveAnchorUtc),
                delay);

            if (existing is null)
            {
                var entity = new PendingScheduledJob
                {
                    UserId = userId,
                    JobKind = JobKind,
                    TargetId = pickemGroupId,
                    SeasonWeek = seasonWeek,
                    WaveAnchorUtc = waveAnchorUtc,
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
                    // A peer scheduler run (concurrent consumer for the same
                    // league-week) inserted first. Detach our orphan, fetch
                    // the winner's row, and continue down the reschedule
                    // branch — if the peer scheduled the same fireTime we
                    // no-op below; otherwise we reschedule. The Hangfire job
                    // we just scheduled becomes the orphan to delete.
                    _dataContext.Entry(entity).State = EntityState.Detached;
                    var winner = await _dataContext.PendingScheduledJobs
                        .FirstAsync(j => j.UserId == userId
                                         && j.JobKind == JobKind
                                         && j.TargetId == pickemGroupId
                                         && j.SeasonWeek == seasonWeek
                                         && j.WaveAnchorUtc == waveAnchorUtc,
                                    cancellationToken);

                    if (winner.ScheduledFireUtc == fireTime)
                    {
                        // Same fireTime — peer already covered it. Clean up
                        // our orphan and exit.
                        TryDeleteHangfireJob(newJobId);
                        return;
                    }

                    // Different fireTime — we take over the scheduling. Old
                    // jobId is the winner's; new is ours.
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
                "Scheduled PickDeadline reminder. UserId={UserId}, FireTime={FireTime}, HangfireJobId={HangfireJobId}",
                userId, fireTime, newJobId);
        }

        // Best-effort cancellation. Hangfire returns false if the job is
        // already in a terminal state — the dispatcher's NotificationLog
        // dedupe absorbs any duplicate fire from a missed delete.
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
            // Npgsql surfaces unique-violation as SQLSTATE 23505.
            return ex.InnerException is Npgsql.PostgresException pg && pg.SqlState == "23505";
        }
    }
}
