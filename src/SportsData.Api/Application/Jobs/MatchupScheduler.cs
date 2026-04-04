using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Common.Jobs;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Jobs
{
    public class MatchupScheduler : IAmARecurringJob
    {
        private readonly ILogger<MatchupScheduler> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ISeasonClientFactory _seasonClientFactory;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public MatchupScheduler(
            ILogger<MatchupScheduler> logger,
            AppDataContext dataContext,
            ISeasonClientFactory seasonClientFactory,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _seasonClientFactory = seasonClientFactory;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task ExecuteAsync()
        {
            // get the current week
            // TODO: multi-sport — resolve sport from context instead of defaulting
            var result = await _seasonClientFactory.Resolve(Sport.FootballNcaa).GetCurrentSeasonWeek();
            var currentWeek = result.IsSuccess ? result.Value : null;

            if (currentWeek is null)
            {
                _logger.LogWarning("Current week could not be found; skipping matchup scheduling");
                return;
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
                    var currentWeekOrdinal = currentWeek.WeekNumber;

                    // if current week is postseason, we need to change the ordinal week number to reflect that
                    if (currentWeek.IsPostSeason)
                    {
                        // we need to get the number of regular season weeks for this season year
                        currentWeekOrdinal = group.Weeks.OrderByDescending(x => x.SeasonWeek)
                            .Take(1).FirstOrDefault()?.SeasonWeek + 1 ?? currentWeek.WeekNumber;
                    }

                    groupWeek = new PickemGroupWeek()
                    {
                        Id = Guid.NewGuid(),
                        AreMatchupsGenerated = false,
                        GroupId = group.Id,
                        SeasonWeek = currentWeekOrdinal,
                        SeasonYear = currentWeek.SeasonYear,
                        SeasonWeekId = currentWeek.Id,
                        IsNonStandardWeek = currentWeek.IsNonStandardWeek
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
                        currentWeek.IsNonStandardWeek,
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
    bool IsNonStandardWeek,
    Guid CorrelationId);
