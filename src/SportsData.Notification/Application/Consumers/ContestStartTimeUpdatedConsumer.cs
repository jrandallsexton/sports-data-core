using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Notification.Application.Scheduling;
using SportsData.Notification.Infrastructure.Data;

namespace SportsData.Notification.Application.Consumers
{
    /// <summary>
    /// ESPN moved a game's start time. Three responsibilities:
    /// <list type="number">
    ///   <item>Update the local <c>PickemGroupMatchup</c> projection — every
    ///   row referencing the contest (a single contest can appear in many
    ///   leagues) gets its <c>StartDateUtc</c> resynced via
    ///   <c>ExecuteUpdateAsync</c>.</item>
    ///   <item>Re-evaluate <c>PickDeadline</c> reminders for every
    ///   (league, week) touched by this contest — the contest's start time
    ///   IS the candidate deadline for any league-week containing it.</item>
    ///   <item>Re-evaluate <c>ContestStart</c> reminders for the contest
    ///   itself — one pass covers every user across every league with the
    ///   contest, as the contest-start scope is per-contest.</item>
    /// </list>
    ///
    /// <para>
    /// Mirrors Producer's
    /// <c>CompetitionStreamScheduler.RescheduleForContestAsync</c> pattern for
    /// the Hangfire side: same drift threshold logic, same crash-safe ordering
    /// (schedule-new → save → delete-old).
    /// </para>
    ///
    /// <para>
    /// Per the cross-broker shovel design, this event arrives via a shovel
    /// from each per-sport Producer broker (NCAA / NFL / MLB). The handler
    /// itself is sport-agnostic — both the projection write and the Hangfire
    /// reschedule operate entirely off ContestId.
    /// </para>
    /// </summary>
    public class ContestStartTimeUpdatedConsumer : IConsumer<ContestStartTimeUpdated>
    {
        private readonly ILogger<ContestStartTimeUpdatedConsumer> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IPickDeadlineReminderScheduler _reminderScheduler;
        private readonly IContestStartReminderScheduler _contestStartScheduler;

        public ContestStartTimeUpdatedConsumer(
            ILogger<ContestStartTimeUpdatedConsumer> logger,
            AppDataContext dataContext,
            IDateTimeProvider dateTimeProvider,
            IPickDeadlineReminderScheduler reminderScheduler,
            IContestStartReminderScheduler contestStartScheduler)
        {
            _logger = logger;
            _dataContext = dataContext;
            _dateTimeProvider = dateTimeProvider;
            _reminderScheduler = reminderScheduler;
            _contestStartScheduler = contestStartScheduler;
        }

        public async Task Consume(ConsumeContext<ContestStartTimeUpdated> context)
        {
            var msg = context.Message;
            using var _ = _logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = msg.CorrelationId,
                ["ContestId"] = msg.ContestId,
                ["NewStartTime"] = msg.NewStartTime,
                ["Sport"] = msg.Sport
            });

            _logger.LogInformation("ContestStartTimeUpdated received.");

            // 1. Resync the local PickemGroupMatchup projection. Bulk
            // ExecuteUpdateAsync avoids loading + tracking; ModifiedUtc /
            // ModifiedBy / StartDateUpdatedAt stamped in the SET clause.
            //
            // Out-of-order delivery guard: only apply when the event's
            // CreatedUtc is newer than the projection's last-applied stamp.
            // Producer's CompetitionStreamScheduler solved this for itself
            // by reading from DB (PR #457); we solve it here by versioning
            // the projection field with the event's own monotonic timestamp.
            // The check lives in the WHERE clause so stale events are
            // filtered at the SQL layer with no read round-trip.
            var now = _dateTimeProvider.UtcNow();
            var eventCreatedUtc = msg.CreatedUtc;
            var rowsAffected = await _dataContext.PickemGroupMatchups
                .Where(m =>
                    m.ContestId == msg.ContestId &&
                    (m.StartDateUpdatedAt == null || m.StartDateUpdatedAt < eventCreatedUtc))
                .ExecuteUpdateAsync(
                    s => s
                        .SetProperty(m => m.StartDateUtc, msg.NewStartTime)
                        .SetProperty(m => m.StartDateUpdatedAt, (DateTime?)eventCreatedUtc)
                        .SetProperty(m => m.ModifiedUtc, (DateTime?)now)
                        .SetProperty(m => m.ModifiedBy, (Guid?)msg.CausationId),
                    context.CancellationToken);

            _logger.LogInformation(
                "PickemGroupMatchup projection resynced. RowsAffected={RowsAffected}, EventCreatedUtc={EventCreatedUtc}",
                rowsAffected, eventCreatedUtc);

            // 2. Re-evaluate pick-deadline reminders for every (league, week)
            // touched by this contest. The same contest can appear in many
            // leagues; the contest's start time IS the candidate deadline
            // for any league-week containing it, so a change requires a
            // re-evaluation pass. We query the projection AFTER the resync
            // above so MIN(StartDateUtc) uses the new value.
            //
            // Unconditional — NOT gated on rowsAffected. Same permanent-miss
            // window as step 3: a crash between ExecuteUpdateAsync and this
            // call would, on redelivery, see StartDateUpdatedAt already at
            // msg.CreatedUtc, filter zero rows, and skip scheduling forever.
            // The PickDeadline scheduler is idempotent (per-user
            // ScheduledFireUtc == fireTime no-op, peer-takeover on 23505),
            // so re-running against the current projection state is safe.
            var affectedScopes = await _dataContext.PickemGroupMatchups
                .AsNoTracking()
                .Where(m => m.ContestId == msg.ContestId)
                .Select(m => new { m.PickemGroupId, m.SeasonWeek })
                .Distinct()
                .ToListAsync(context.CancellationToken);

            foreach (var scope in affectedScopes)
            {
                await _reminderScheduler.EvaluateAndScheduleForLeagueWeekAsync(
                    scope.PickemGroupId, scope.SeasonWeek, context.CancellationToken);
            }

            // 3. Re-evaluate ContestStart reminders for every user across every
            // league containing this contest. Unconditional for the same
            // reason as step 2 — gating on rowsAffected would leak a
            // permanent-miss window between ExecuteUpdateAsync and this call.
            await _contestStartScheduler.EvaluateAndScheduleForContestAsync(
                msg.ContestId, context.CancellationToken);
        }
    }
}
