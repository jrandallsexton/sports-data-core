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
        /// worth a scoring attempt. Comfortably longer than any game plus
        /// overtime or a weather delay — being late costs one job interval,
        /// being early costs a pointless round trip on every run.
        /// </summary>
        private const int PlayableWindowHours = 6;

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

                var unscoredContestIds = await _dataContext.UserPicks
                    .Where(p => p.ScoredAt == null)
                    .Join(
                        _dataContext.PickemGroupMatchups,
                        pick => pick.ContestId,
                        matchup => matchup.ContestId,
                        (pick, matchup) => new { pick.ContestId, matchup.StartDateUtc })
                    .Where(x => x.StartDateUtc <= startedBefore)
                    .Select(x => x.ContestId)
                    .Distinct()
                    .ToListAsync();

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
