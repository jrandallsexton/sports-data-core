using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Enums;
using SportsData.Producer.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Contests
{
    public interface IEnrichContests
    {
        Task Process(EnrichContestCommand command);
    }

    public class ContestEnrichmentProcessor : IEnrichContests
    {
        private readonly ILogger<ContestEnrichmentProcessor> _logger;
        private readonly FootballDataContext _dataContext;
        private readonly IEventBus _bus;
        private readonly IDateTimeProvider _dateTimeProvider;

        public ContestEnrichmentProcessor(
            ILogger<ContestEnrichmentProcessor> logger,
            FootballDataContext dataContext,
            IEventBus bus,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
            _dateTimeProvider = dateTimeProvider;
        }

        public async Task Process(EnrichContestCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                _logger.LogInformation("Contest enrichment job started for {@command}", command);

                var competition = await _dataContext.Competitions
                    .Include(c => c.Competitors)
                    .Include(c => c.Odds)
                    .ThenInclude(o => o.Teams)
                    .Include(c => c.Contest)
                    .Include(c => c.Status)
                    .Where(c => c.ContestId == command.ContestId)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync();

                if (competition is null)
                {
                    _logger.LogError("Competition could not be loaded for provided contest id. {@Command}", command);
                    return;
                }

                var awayCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway == "away");
                var homeCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway == "home");

                if (awayCompetitor is null || homeCompetitor is null)
                {
                    _logger.LogError(
                        "Competition is missing away or home competitor. ContestId={ContestId}, Away={HasAway}, Home={HasHome}",
                        command.ContestId, awayCompetitor is not null, homeCompetitor is not null);
                    return;
                }

                // If canonical status is not final, data isn't ready — return cleanly
                if (competition.Status?.StatusTypeName != "STATUS_FINAL")
                {
                    _logger.LogInformation(
                        "Contest status is not yet final for {ContestName}. Current: {Status}. Skipping enrichment.",
                        competition.Contest.Name, competition.Status?.StatusTypeName ?? "unknown");
                    return;
                }

                var contest = competition.Contest;

                // Query canonical plays to determine final score
                var lastScoringPlay = await _dataContext.CompetitionPlays
                    .AsNoTracking()
                    .Where(p => p.CompetitionId == competition.Id && p.ScoringPlay)
                    .OrderByDescending(p => p.PeriodNumber)
                    .ThenBy(p => p.ClockValue)
                    .FirstOrDefaultAsync();

                if (lastScoringPlay != null)
                {
                    contest.AwayScore = lastScoringPlay.AwayScore;
                    contest.HomeScore = lastScoringPlay.HomeScore;
                }
                else
                {
                    // No scoring plays — check competitor scores (D2 fallback)
                    var awayFinalScore = await _dataContext.CompetitionCompetitorScores
                        .AsNoTracking()
                        .Where(s => s.CompetitionCompetitorId == awayCompetitor.Id)
                        .OrderByDescending(s => s.SourceDescription == "Final" ? 1 : 0)
                        .ThenByDescending(s => s.CreatedUtc)
                        .FirstOrDefaultAsync();

                    var homeFinalScore = await _dataContext.CompetitionCompetitorScores
                        .AsNoTracking()
                        .Where(s => s.CompetitionCompetitorId == homeCompetitor.Id)
                        .OrderByDescending(s => s.SourceDescription == "Final" ? 1 : 0)
                        .ThenByDescending(s => s.CreatedUtc)
                        .FirstOrDefaultAsync();

                    if (awayFinalScore is null || homeFinalScore is null)
                    {
                        _logger.LogInformation(
                            "No plays or competitor scores available for {ContestName}. Data not ready for enrichment.",
                            contest.Name);
                        return;
                    }

                    contest.AwayScore = (int)awayFinalScore.Value;
                    contest.HomeScore = (int)homeFinalScore.Value;
                }

                var awayFranchiseSeasonId = awayCompetitor.FranchiseSeasonId;
                var homeFranchiseSeasonId = homeCompetitor.FranchiseSeasonId;

                if (contest.AwayScore != contest.HomeScore)
                {
                    var homeWasWinner = contest.AwayScore < contest.HomeScore;

                    contest.WinnerFranchiseId =
                        homeWasWinner ?
                        homeFranchiseSeasonId :
                        awayFranchiseSeasonId;
                }

                contest.FinalizedUtc = _dateTimeProvider.UtcNow();

                // Enrich results for every odds provider
                if (competition.Odds?.Any() == true)
                {
                    EnrichOddsResults(
                        competition.Odds,
                        awayFranchiseSeasonId,
                        homeFranchiseSeasonId,
                        contest.AwayScore!.Value,
                        contest.HomeScore!.Value);

                    // Maintain Contest-level denormalized fields from the primary provider
                    var primaryOdds = competition.Odds
                        .FirstOrDefault(o => o.EnrichedUtc.HasValue && o.ProviderId == SportsBook.EspnBet.ToProviderId())
                        ?? competition.Odds.FirstOrDefault(o => o.EnrichedUtc.HasValue);

                    if (primaryOdds != null)
                    {
                        contest.OverUnder = primaryOdds.OverUnderResult;
                        contest.SpreadWinnerFranchiseId = primaryOdds.AtsWinnerFranchiseSeasonId;
                    }
                }

                await _bus.Publish(
                    new ContestEnrichmentCompleted(
                        command.ContestId,
                        null,
                        contest.Sport,
                        contest.SeasonYear,
                        command.CorrelationId,
                        Guid.NewGuid()));
                await _dataContext.SaveChangesAsync();
            }
        }

        internal void EnrichOddsResults(
            ICollection<CompetitionOdds> allOdds,
            Guid awayFranchiseSeasonId,
            Guid homeFranchiseSeasonId,
            int awayScore,
            int homeScore)
        {
            foreach (var odds in allOdds)
            {
                // Reset prior results so reruns don't retain stale values
                odds.WinnerFranchiseSeasonId = null;
                odds.AtsWinnerFranchiseSeasonId = null;
                odds.OverUnderResult = OverUnderResult.None;

                // Straight-up winner
                if (awayScore != homeScore)
                {
                    odds.WinnerFranchiseSeasonId = homeScore > awayScore
                        ? homeFranchiseSeasonId
                        : awayFranchiseSeasonId;
                }

                // Over/Under result
                if (odds.OverUnder.HasValue)
                {
                    odds.OverUnderResult = GetOverUnderResult(awayScore, homeScore, odds.OverUnder.Value);
                }

                // ATS winner
                if (odds.Spread.HasValue)
                {
                    odds.AtsWinnerFranchiseSeasonId = GetSpreadWinnerFranchiseSeasonId(
                        awayFranchiseSeasonId,
                        homeFranchiseSeasonId,
                        awayScore,
                        homeScore,
                        odds.Spread.Value);
                }

                odds.EnrichedUtc = _dateTimeProvider.UtcNow();

                _logger.LogInformation(
                    "Enriched CompetitionOdds {OddsId} for provider {ProviderName}. " +
                    "Winner={WinnerId}, ATS={AtsId}, O/U={OverUnder}",
                    odds.Id, odds.ProviderName,
                    odds.WinnerFranchiseSeasonId,
                    odds.AtsWinnerFranchiseSeasonId,
                    odds.OverUnderResult);
            }
        }

        internal OverUnderResult GetOverUnderResult(int awayScore, int homeScore, decimal overUnder)
        {
            var total = awayScore + homeScore;

            if (total > overUnder)
                return OverUnderResult.Over;

            if (total < overUnder)
                return OverUnderResult.Under;

            return OverUnderResult.Push;
        }

        internal Guid? GetSpreadWinnerFranchiseSeasonId(
            Guid awayFranchiseSeasonId,
            Guid homeFranchiseSeasonId,
            int awayScore,
            int homeScore,
            decimal spread)
        {
            var adjustedHomeScore = homeScore + spread;
            var margin = adjustedHomeScore - awayScore;

            return margin switch
            {
                > 0 => homeFranchiseSeasonId,
                < 0 => awayFranchiseSeasonId,
                _ => Guid.Empty
            };
        }
    }
}
