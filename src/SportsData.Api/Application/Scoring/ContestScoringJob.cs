using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Scoring
{
    public class ContestScoringJob : IAmARecurringJob
    {
        private readonly ILogger<ContestScoringJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalData;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly Guid _correlationId = Guid.NewGuid();

        public ContestScoringJob(
            ILogger<ContestScoringJob> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalData,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalData = canonicalData;
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

                await ExecuteInternal();
            }
        }

        private async Task ExecuteInternal()
        {
            // get the current week
            var seasonWeeks = await _canonicalData.GetCurrentAndLastWeekSeasonWeeks();

            foreach (var seasonWeek in seasonWeeks)
            {
                // get a distinct list of all contests associated with UserPick records that have not been scored
                var unscoredContestIds = await _dataContext.UserPicks
                    .Where(p => p.ScoredAt == null)
                    .Select(p => p.ContestId)
                    .Distinct()
                    .ToListAsync();

                // get a list of all contests for the week that have been finalized
                var contestIdsReadyToScore = await _canonicalData
                    .GetFinalizedContestIds(seasonWeek.Id);

                // determine if they have been enriched
                var contestIdsToScore = unscoredContestIds
                    .Where(x => contestIdsReadyToScore.Contains(x));

                // send them each for scoring (generating UserPick points)
                foreach (var contestId in contestIdsToScore)
                {
                    var cmd = new ScoreContestCommand(contestId);
                    _backgroundJobProvider.Enqueue<IScoreContests>(p => p.Process(cmd));
                }
            }
        }
    }
}
