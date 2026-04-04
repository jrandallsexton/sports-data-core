using SportsData.Api.Application.Contests;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;
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
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestRecapJob(
            ILogger<ContestRecapJob> logger,
            ISeasonClientFactory seasonClientFactory,
            IProvideCanonicalData canonicalDataProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _seasonClientFactory = seasonClientFactory;
            _canonicalDataProvider = canonicalDataProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // get the current and last season weeks
            // TODO: multi-sport
            var weeksResult = await _seasonClientFactory.Resolve(SportsData.Core.Common.Sport.FootballNcaa).GetCurrentAndLastSeasonWeeks();
            var weeks = weeksResult.IsSuccess ? weeksResult.Value : [];

            if (weeks.Count == 0)
            {
                _logger.LogWarning("Could not determine current or last week for contest recaps.");
                return;
            }

            foreach (var week in weeks)
            {
                var completedContestIds = await _canonicalDataProvider
                    .GetCompletedFbsContestIdsBySeasonWeekId(week.Id);

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
