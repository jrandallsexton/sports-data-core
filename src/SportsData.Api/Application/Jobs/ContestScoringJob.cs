using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
{
    /// <summary>
    /// Daily backstop for the live contest-scoring path.
    ///
    /// Primary scoring trigger is event-driven (Producer publishes
    /// <c>ContestCompleted</c> on STATUS_FINAL → API <c>ContestCompletedHandler</c>
    /// enqueues <see cref="ContestScoringProcessor"/>). This job is the
    /// safety net for events lost in transit (broker outage, consumer pod
    /// restart, admin replay races, etc.).
    ///
    /// Sport-agnostic by construction: we enqueue a <see cref="ScoreContestCommand"/>
    /// for every distinct contest that still has unscored picks, regardless of
    /// sport. The processor (PR-N+1) resolves sport per-contest via
    /// <c>PickemGroup.Sport</c>, checks finalization through the sport-specific
    /// <c>ContestClient</c>, and short-circuits cleanly when there's nothing
    /// to do — so this job stays a thin "enqueue all candidates" pass.
    /// </summary>
    public class ContestScoringJob : IAmARecurringJob
    {
        private readonly ILogger<ContestScoringJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly Guid _correlationId = Guid.NewGuid();

        public ContestScoringJob(
            ILogger<ContestScoringJob> logger,
            AppDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = _correlationId
            }))
            {
                _logger.LogInformation("{MethodName} Began", nameof(ContestScoringJob));

                var unscoredContestIds = await _dataContext.UserPicks
                    .Where(p => p.ScoredAt == null)
                    .Select(p => p.ContestId)
                    .Distinct()
                    .ToListAsync();

                _logger.LogInformation(
                    "Found {Count} distinct contests with unscored picks. Enqueuing scoring for each.",
                    unscoredContestIds.Count);

                foreach (var contestId in unscoredContestIds)
                {
                    var cmd = new ScoreContestCommand(contestId, _correlationId);
                    _backgroundJobProvider.Enqueue<IScoreContests>(p => p.Process(cmd));
                }

                _logger.LogInformation("{MethodName} Ended", nameof(ContestScoringJob));
            }
        }
    }
}
