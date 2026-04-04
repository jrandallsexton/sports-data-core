using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
{
    public class ContestScoringJob : IAmARecurringJob
    {
        private readonly ILogger<ContestScoringJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ISeasonClientFactory _seasonClientFactory;
        private readonly IContestClientFactory _contestClientFactory;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly Guid _correlationId = Guid.NewGuid();

        public ContestScoringJob(
            ILogger<ContestScoringJob> logger,
            AppDataContext dataContext,
            ISeasonClientFactory seasonClientFactory,
            IContestClientFactory contestClientFactory,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _seasonClientFactory = seasonClientFactory;
            _contestClientFactory = contestClientFactory;
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
            // TODO: multi-sport
            var weeksResult = await _seasonClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa).GetCurrentAndLastSeasonWeeks();
            if (!weeksResult.IsSuccess)
            {
                _logger.LogWarning("Failed to retrieve season weeks from Producer. Will retry on next run.");
                return;
            }

            var seasonWeeks = weeksResult.Value;

            foreach (var seasonWeek in seasonWeeks)
            {
                // get a distinct list of all contests associated with UserPick records that have not been scored
                var unscoredContestIds = await _dataContext.UserPicks
                    .Where(p => p.ScoredAt == null)
                    .Select(p => p.ContestId)
                    .Distinct()
                    .ToListAsync();

                // get a list of all contests for the week that have been finalized
                var finalizedResult = await _contestClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa)
                    .GetFinalizedContestIds(seasonWeek.Id);
                if (!finalizedResult.IsSuccess)
                {
                    _logger.LogWarning("Failed to retrieve finalized contests for week {WeekId}. Skipping.", seasonWeek.Id);
                    continue;
                }
                var contestIdsReadyToScore = finalizedResult.Value;

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
