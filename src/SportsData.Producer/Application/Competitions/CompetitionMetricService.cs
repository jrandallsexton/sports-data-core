using MassTransit;
using Microsoft.EntityFrameworkCore;

using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;

namespace SportsData.Producer.Application.Competitions
{
    public class CompetitionMetricService
    {
        private readonly ILogger<CompetitionMetricService> _logger;
        private readonly TeamSportDataContext _dataContext;

        public CompetitionMetricService(
            ILogger<CompetitionMetricService> logger,
            TeamSportDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }


        public async Task CalculateCompetitionMetrics(Guid competitionId)
        {
            var competition = await _dataContext.Competitions
                .AsNoTracking()
                .Include(x => x.Contest)
                .ThenInclude(c => c.AwayTeamFranchiseSeason)
                .Include(x => x.Contest)
                .ThenInclude(c => c.HomeTeamFranchiseSeason)
                .Include(x => x.Plays.OrderBy(p => p.SequenceNumber))
                .FirstOrDefaultAsync(x => x.Id == competitionId);

            if (competition == null)
            {
                _logger.LogError("Competition not found");
                return;
            }

            var awayFranchiseSeasonId = competition.Contest.AwayTeamFranchiseSeasonId;
            var homeFranchiseSeasonId = competition.Contest.HomeTeamFranchiseSeasonId;
        }

        private (CompetitionMetric, CompetitionMetric) CalculateMetrics(
            List<CompetitionPlay> plays,
            Guid awayFranchiseSeasonId,
            Guid homeFranchiseSeasonId)
        {
            var awayMetric = new CompetitionMetric();
            awayMetric.Ypp = CalculateYpp(awayFranchiseSeasonId, plays);

            var homeMetric = new CompetitionMetric();
            homeMetric.Ypp = CalculateYpp(homeFranchiseSeasonId, plays);

            throw new NotImplementedByDesignException();
        }

        private decimal CalculateYpp(Guid franchiseSeasonId, List<CompetitionPlay> plays)
        {
            // TODO: Implement Yards Per Play calculation logic
            throw new NotImplementedByDesignException();
        }
    }
}
