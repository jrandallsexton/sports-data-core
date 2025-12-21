using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Processing;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Contests
{
    /// <summary>
    /// Gets a list of contestIds for the current season week that need to be updated
    /// and enqueues jobs to update them.
    /// </summary>
    public class ContestUpdateJob : IAmARecurringJob
    {
        private readonly ILogger<ContestUpdateJob> _logger;
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public ContestUpdateJob(
            ILogger<ContestUpdateJob> logger,
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
            var currentSeasonWeek = await _dataContext.SeasonWeeks
                .Include(w => w.Season)
                .AsNoTracking()
                .Where(sw => sw.StartDate < DateTime.UtcNow &&
                             sw.EndDate > DateTime.UtcNow)
                .FirstOrDefaultAsync();

            if (currentSeasonWeek is null)
            {
                _logger.LogError("Could not determine current season week");
                return;
            }

            // get all contests in this season week
            var contests = await _dataContext.Contests
                .AsNoTracking()
                .Where(c => c.SeasonWeekId == currentSeasonWeek.Id)
                .OrderBy(c => c.StartDateUtc)
                .ToListAsync();

            // spawn a job to update each
            foreach (var contest in contests)
            {
                var cmd = new UpdateContestCommand(
                    contest.Id,
                    SourceDataProvider.Espn,
                    Sport.FootballNcaa,
                    Guid.NewGuid());
                _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
            }
        }
    }
}
