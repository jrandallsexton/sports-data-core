using Microsoft.EntityFrameworkCore;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
{
    public class MatchupScheduler
    {
        private readonly ILogger<MatchupScheduler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalDataProvider;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public MatchupScheduler(
            ILogger<MatchupScheduler> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalDataProvider,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalDataProvider = canonicalDataProvider;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // get the current week
            var currentWeek = await _canonicalDataProvider.GetCurrentSeasonWeek();

            if (currentWeek is null)
            {
                _logger.LogError("Current week could not be found");
                throw new Exception("Current week could not be found");
            }

            // find all PickemGroup entities who do not yet have a PickemGroupWeek for this week
            var groups = await _dataContext.PickemGroups
                .Include(x => x.Weeks)
                .OrderBy(x => x.CreatedUtc)
                .ToListAsync();

            foreach (var group in groups)
            {
                var groupWeek = group.Weeks.FirstOrDefault(x => x.SeasonWeekId == currentWeek.Id);

                if (groupWeek is null)
                {
                    groupWeek = new PickemGroupWeek()
                    {
                        Id = Guid.NewGuid(),
                        AreMatchupsGenerated = false,
                        GroupId = group.Id,
                        SeasonWeek = currentWeek.WeekNumber,
                        SeasonYear = currentWeek.SeasonYear,
                        SeasonWeekId = currentWeek.Id
                    };
                    group.Weeks.Add(groupWeek);
                    await _dataContext.SaveChangesAsync();
                }

                if (!groupWeek.AreMatchupsGenerated)
                {
                    var cmd = new ScheduleGroupWeekMatchupsCommand(
                        group.Id,
                        currentWeek.Id,
                        currentWeek.SeasonYear,
                        currentWeek.WeekNumber,
                        Guid.NewGuid());
                    _backgroundJobProvider.Enqueue<IScheduleGroupWeekMatchups>(p => p.Process(cmd));
                }
            }
        }
    }
}

public record ScheduleGroupWeekMatchupsCommand(
    Guid GroupId,
    Guid SeasonWeekId,
    int SeasonYear,
    int SeasonWeek,
    Guid CorrelationId);
