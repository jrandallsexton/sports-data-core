using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
{
    /// <summary>
    /// Daily backstop for the live contest-scoring path.
    ///
    /// Primary scoring trigger is event-driven (Producer publishes
    /// <c>ContestCompleted</c> on STATUS_FINAL → API <c>ContestCompletedHandler</c>
    /// enqueues <see cref="PickScoringProcessor"/>). This job is the
    /// safety net for events lost in transit (broker outage, consumer pod
    /// restart, admin replay races, etc.).
    ///
    /// Sport-agnostic by construction: we enqueue a <see cref="ScorePicksCommand"/>
    /// for every distinct contest that still has unscored picks, regardless of
    /// sport. The processor (PR-N+1) resolves sport per-contest via
    /// <c>PickemGroup.Sport</c>, checks finalization through the sport-specific
    /// <c>ContestClient</c>, and short-circuits cleanly when there's nothing
    /// to do — so this job stays a thin "enqueue all candidates" pass.
    /// </summary>
    public class PickScoringJob : IAmARecurringJob
    {
        /// <summary>
        /// Hours after kickoff before a contest is considered playable-out and
        /// worth a scoring attempt. An NFL game runs about three hours, and
        /// still comfortably under four with overtime, so four clears a
        /// finished game without waiting on one. Erring short is cheap: an
        /// early attempt short-circuits cleanly on the NotFound relay and is
        /// retried next pass, whereas erring long delays scoring for every
        /// game that the event-driven ContestCompleted path happened to miss.
        /// </summary>
        private const int PlayableWindowHours = 4;

        private readonly ILogger<PickScoringJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly Guid _correlationId = Guid.NewGuid();

        public PickScoringJob(
            ILogger<PickScoringJob> logger,
            AppDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task ExecuteAsync()
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = _correlationId
            }))
            {
                _logger.LogInformation("{MethodName} Began", nameof(PickScoringJob));

                // Only contests whose game can plausibly be OVER. Without this
                // the pass enqueued every contest anyone had picked, including
                // games days away — during a season that is most of the slate,
                // and each one round-trips to Producer to be told there is no
                // result yet. The processor short-circuits on that, so this is
                // wasted work rather than incorrect work, but the volume is
                // real: it repeats on every run until the game is played.
                //
                // A generous lower bound (kickoff + this window) rather than a
                // finalization check, which only Producer can answer. Games run
                // long — overtime, weather delays — so the window errs late; a
                // contest that finished sooner simply gets scored on the next
                // pass, and the event-driven path (ContestCompleted) has
                // already handled the common case anyway.
                var startedBefore = _dateTimeProvider.UtcNow().AddHours(-PlayableWindowHours);

                // Joined on GROUP AND CONTEST, not contest alone. The matchup
                // row is per-group, so a contest picked in ten leagues has ten
                // rows: a contest-only join fans each pick out across all of
                // them, and lets one group's row (which could carry a stale
                // StartDateUtc after a reschedule) admit another group's pick.
                // Keying on the pick's own group keeps it 1:1 and correct.
                var unscoredContestIds = await _dataContext.UserPicks
                    .AsNoTracking()
                    .Where(p => p.ScoredAt == null
                        && _dataContext.PickemGroupMatchups.Any(m =>
                            m.GroupId == p.PickemGroupId
                            && m.ContestId == p.ContestId
                            && m.StartDateUtc <= startedBefore))
                    .Select(p => p.ContestId)
                    .Distinct()
                    .ToListAsync();

                // Deliberately enqueued per CONTEST, not per group, even
                // though eligibility was evaluated per group above. Kickoff is
                // a property of the game, so two groups' rows for one contest
                // should agree; if they disagree one is stale, and either way
                // this cannot score a game early — PickScoringProcessor
                // refuses to score unless the matchup result carries
                // FinalizedUtc (see the 2026-06-16 incident noted there).
                // Scoring per group would multiply Producer round trips by the
                // number of leagues sharing a contest to guard something the
                // processor already guarantees.

                _logger.LogInformation(
                    "Found {Count} distinct contests with unscored picks whose game started before {StartedBefore}. Enqueuing scoring for each.",
                    unscoredContestIds.Count,
                    startedBefore);

                foreach (var contestId in unscoredContestIds)
                {
                    var cmd = new ScorePicksCommand(contestId, _correlationId);
                    _backgroundJobProvider.Enqueue<IScorePicks>(p => p.Process(cmd));
                }

                _logger.LogInformation("{MethodName} Ended", nameof(PickScoringJob));
            }
        }
    }
}
