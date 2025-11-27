using SportsData.Api.Application.Contests;
using SportsData.Api.Infrastructure.Data.Canonical;
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
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestRecapJob(
            ILogger<ContestRecapJob> logger,
            IProvideCanonicalData canonicalDataProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _canonicalDataProvider = canonicalDataProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // get the current and last season weeks
            var weeks = await _canonicalDataProvider.GetCurrentAndLastWeekSeasonWeeks();

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
