using SportsData.Api.Application.Contests;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
{
    /// <summary>
    /// Cron-base job for generating contest recaps from ContestOverview DTOs
    /// </summary>
    public class ContestRecapJob : IAmARecurringJob
    {
        private readonly ILogger<ContestRecapJob> _logger;
        private readonly ISeasonClientFactory _seasonClientFactory;
        private readonly IContestClientFactory _contestClientFactory;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestRecapJob(
            ILogger<ContestRecapJob> logger,
            ISeasonClientFactory seasonClientFactory,
            IContestClientFactory contestClientFactory,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _seasonClientFactory = seasonClientFactory;
            _contestClientFactory = contestClientFactory;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // get the current and last season weeks
            // TODO: multi-sport
            var weeksResult = await _seasonClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa).GetCurrentAndLastSeasonWeeks();
            if (!weeksResult.IsSuccess)
            {
                _logger.LogWarning("Failed to retrieve season weeks from Producer. Will retry on next run.");
                return;
            }

            var weeks = weeksResult.Value;

            if (weeks.Count == 0)
            {
                _logger.LogWarning("Could not determine current or last week for contest recaps.");
                return;
            }

            foreach (var week in weeks)
            {
                var fbsResult = await _contestClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa)
                    .GetCompletedFbsContestIds(week.Id);
                if (!fbsResult.IsSuccess)
                {
                    _logger.LogWarning("Failed to retrieve completed FBS contests for week {WeekId}. Skipping.", week.Id);
                    continue;
                }
                var completedContestIds = fbsResult.Value;

                foreach (var contestId in completedContestIds)
                {
                    _backgroundJobProvider.Schedule<ContestRecapProcessor>(x =>
                        x.ProcessAsync(contestId), TimeSpan.FromSeconds(30));

                    await Task.Delay(1000);
                }
            }
        }
    }
}
