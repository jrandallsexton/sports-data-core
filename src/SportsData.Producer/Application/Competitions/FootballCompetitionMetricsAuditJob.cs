using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;

namespace SportsData.Producer.Application.Competitions
{
    public class FootballCompetitionMetricsAuditJob : IAmARecurringJob
    {
        private readonly ILogger<FootballCompetitionMetricsAuditJob> _logger;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public FootballCompetitionMetricsAuditJob(
            ILogger<FootballCompetitionMetricsAuditJob> logger,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _backgroundJobProvider = backgroundJobProvider;
        }


        public Task ExecuteAsync()
        {
            throw new NotImplementedException();
        }
    }
}
