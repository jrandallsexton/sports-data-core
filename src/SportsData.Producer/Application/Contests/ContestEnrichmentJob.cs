using Microsoft.EntityFrameworkCore;

using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    public class ContestEnrichmentJob : IAmARecurringJob
    {
        private readonly ILogger<ContestEnrichmentJob> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestEnrichmentJob(
            ILogger<ContestEnrichmentJob> logger,
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // get the current season week
            var seasonWeek = await _dataContext.SeasonWeeks
                .AsNoTracking()
                .Include(sw => sw.Season)
                .Where(sw => sw.StartDate < DateTime.UtcNow && sw.EndDate > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (seasonWeek is null)
            {
                _logger.LogError("Could not determine current season week");
                return;
            }

            // get contests that have not been finalized
            var contests = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.SeasonWeekId == seasonWeek.Id &&
                            c.StartDateUtc < DateTime.UtcNow &&
                            c.FinalizedUtc == null)
                .OrderBy(c => c.StartDateUtc)
                .ToListAsync();

            // spawn a job to finalize each
            foreach (var contest in contests)
            {
                var cmd = new EnrichContestCommand()
                {
                    ContestId = contest.Id
                };
                _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
            }
        }
    }

    public interface IAmARecurringJob
    {
        Task ExecuteAsync();
    }

    public class EnrichContestCommand
    {
        public Guid ContestId { get; set; }

        public Guid CorrelationId { get; set; } = Guid.NewGuid();
    }
}
