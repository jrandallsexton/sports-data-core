using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Contests;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.SeasonWeek
{
    public interface ISeasonWeekService
    {
        Task UpdateSeasonWeekContests(Guid seasonWeekId);
    }

    public class SeasonWeekService : ISeasonWeekService
    {
        private readonly TeamSportDataContext _dataContext;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public SeasonWeekService(
            TeamSportDataContext dataContext,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _dataContext = dataContext;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task UpdateSeasonWeekContests(Guid seasonWeekId)
        {
            var contestIds = await _dataContext.Contests
                .Where(c => c.SeasonWeekId == seasonWeekId)
                .Select(x => x.Id)
                .ToListAsync();

            foreach (var contestId in contestIds)
            {
                var cmd = new UpdateContestCommand(
                    contestId,
                    SourceDataProvider.Espn,
                    Sport.FootballNcaa,
                    Guid.NewGuid());
                _backgroundJobProvider.Enqueue<IUpdateContests>(p => p.Process(cmd));
            }
        }
    }
}
