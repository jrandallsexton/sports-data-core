using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Competitions.Commands.CalculateCompetitionMetrics;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions
{
    public class FootballCompetitionMetricsAuditJob : IAmARecurringJob
    {
        private readonly ILogger<FootballCompetitionMetricsAuditJob> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;
        private readonly IDateTimeProvider _dateTimeProvider;

        public FootballCompetitionMetricsAuditJob(
            ILogger<FootballCompetitionMetricsAuditJob> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider,
            IDateTimeProvider dateTimeProvider
            )
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
            _dateTimeProvider = dateTimeProvider;
        }


        public async Task ExecuteAsync()
        {
            var competitionsWithoutMetrics = await _dataContext.Competitions
                .AsNoTracking()
                .Include(x => x.Metrics)
                .Where(x => x.Date < _dateTimeProvider.UtcNow().AddHours(-3) &&
                            !x.Metrics.Any())
                .Select(x => x.Id)
                .ToListAsync();

            if (!competitionsWithoutMetrics.Any())
            {
                _logger.LogInformation("All football competitions have metrics.");
                return;
            }

            foreach (var competitionId in competitionsWithoutMetrics)
            {
                var command = new CalculateCompetitionMetricsCommand(competitionId);
                _backgroundJobProvider.Enqueue<ICalculateCompetitionMetricsCommandHandler>(
                    h => h.ExecuteAsync(command, CancellationToken.None));
            }
        }
    }
}
