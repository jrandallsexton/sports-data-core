using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Contests
{
    // Same Hangfire reflection-resolve issue that bit ContestUpdateJob — register against
    // this non-generic interface so the recurring-job entry stores a stable type name.
    public interface IContestEnrichmentJob
    {
        Task ExecuteAsync();
    }

    public class ContestEnrichmentJob<TDataContext> : IContestEnrichmentJob, IAmARecurringJob
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<ContestEnrichmentJob<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestEnrichmentJob(
            ILogger<ContestEnrichmentJob<TDataContext>> logger,
            TDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // get the current and previous season week
            var seasonWeeks = await _dataContext.SeasonWeeks
                .AsNoTracking()
                .Where(sw => sw.StartDate < DateTime.UtcNow &&
                             sw.EndDate > DateTime.UtcNow)
                .OrderByDescending(sw => sw.StartDate)
                .Take(2)
                .ToListAsync();

            if (!seasonWeeks.Any())
            {
                _logger.LogError("Could not determine current season week");
                return;
            }

            foreach (var seasonWeek in seasonWeeks)
            {
                // get contests that have not been finalized
                var contests = await _dataContext.Contests
                    .AsNoTracking()
                    .Where(c => c.SeasonWeekId == seasonWeek.Id &&
                                c.StartDateUtc < DateTime.UtcNow.AddHours(3) &&
                                c.FinalizedUtc == null)
                    .OrderBy(c => c.StartDateUtc)
                    .ToListAsync();

                // spawn a job to finalize each
                foreach (var contest in contests)
                {
                    var cmd = new EnrichContestCommand(contest.Id, Guid.NewGuid());
                    _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
                }
            }
        }
    }
}
