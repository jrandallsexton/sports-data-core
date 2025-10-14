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
            // get the current and previous season week
            var seasonWeeks = await _dataContext.SeasonWeeks
                .AsNoTracking()
                .Where(sw => sw.StartDate < DateTime.UtcNow &&
                             sw.EndDate > DateTime.UtcNow.AddDays(-13))
                .OrderByDescending(sw => sw.Number)
                .Take(2)
                .ToListAsync();

            //var seasonWeeks = await _dataContext.SeasonWeeks
            //    .AsNoTracking()
            //    .Where(sw => sw.StartDate < DateTime.UtcNow &&
            //                 sw.EndDate > DateTime.UtcNow)
            //    .Take(2)
            //    .ToListAsync();

            if (!seasonWeeks.Any())
            {
                _logger.LogError("Could not determine current season week");
                return;
            }

            foreach (var seasonWeek in seasonWeeks)
            {
                // get contests that have not been finalized
                //var contests = await _dataContext.Contests
                //    .AsNoTracking()
                //    .Where(c => c.SeasonWeekId == seasonWeek.Id &&
                //                c.StartDateUtc < DateTime.UtcNow.AddHours(3))
                //    .OrderBy(c => c.StartDateUtc)
                //    .ToListAsync();

                var contests = await _dataContext.Contests
                    .AsNoTracking()
                    .Where(c => c.SeasonWeekId == seasonWeek.Id &&
                                c.StartDateUtc < DateTime.UtcNow.AddHours(3) &&
                                c.FinalizedUtc == null)
                    .OrderBy(c => c.StartDateUtc)
                    .ToListAsync();

                // spawn a job to finalize each
                foreach (var contest in contests.Take(1))
                {
                    var cmd = new EnrichContestCommand(contest.Id, Guid.NewGuid());
                    _backgroundJobProvider.Enqueue<IEnrichContests>(p => p.Process(cmd));
                }
            }
        }
    }

    public interface IAmARecurringJob
    {
        Task ExecuteAsync();
    }
}
