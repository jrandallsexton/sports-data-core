using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Contests
{
    public class FootballContestEnrichmentProcessor : ContestEnrichmentProcessor<FootballDataContext>
    {
        public FootballContestEnrichmentProcessor(
            ILogger<ContestEnrichmentProcessor<FootballDataContext>> logger,
            FootballDataContext dataContext,
            IEventBus bus,
            IDateTimeProvider dateTimeProvider)
            : base(logger, dataContext, bus, dateTimeProvider)
        {
        }

        // Football-specific: prefer the last scoring play (uses ClockValue tiebreak,
        // which only exists on FootballCompetitionPlay). Falls back to the canonical
        // score record path when no scoring plays are present (e.g., D2 sources).
        protected override async Task<(int? Away, int? Home)> GetFinalScoresAsync(
            Guid competitionId,
            Guid awayCompetitorId,
            Guid homeCompetitorId)
        {
            var lastScoringPlay = await _dataContext.CompetitionPlays
                .AsNoTracking()
                .Where(p => p.CompetitionId == competitionId && p.ScoringPlay)
                .OrderByDescending(p => p.PeriodNumber)
                .ThenBy(p => p.ClockValue)
                .Select(p => new { p.AwayScore, p.HomeScore })
                .FirstOrDefaultAsync();

            if (lastScoringPlay != null)
            {
                return (lastScoringPlay.AwayScore, lastScoringPlay.HomeScore);
            }

            return await base.GetFinalScoresAsync(competitionId, awayCompetitorId, homeCompetitorId);
        }
    }
}
