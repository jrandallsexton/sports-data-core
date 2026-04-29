using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Producer.Enums;
using SportsData.Producer.Extensions;
using SportsData.Producer.Infrastructure.Data.Baseball;

namespace SportsData.Producer.Application.Contests
{
    public class BaseballContestEnrichmentProcessor : IEnrichContests
    {
        private readonly ILogger<BaseballContestEnrichmentProcessor> _logger;
        private readonly BaseballDataContext _dataContext;
        private readonly IEventBus _bus;
        private readonly IDateTimeProvider _dateTimeProvider;

        public BaseballContestEnrichmentProcessor(
            ILogger<BaseballContestEnrichmentProcessor> logger,
            BaseballDataContext dataContext,
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

                var status = await _dataContext.CompetitionStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.CompetitionId == competition.Id);

                if (status?.StatusTypeName != "STATUS_FINAL")
                {
                    _logger.LogInformation(
                        "Contest status is not yet final for {ContestName}. Current: {Status}. Skipping enrichment.",
                        competition.Contest.Name, status?.StatusTypeName ?? "unknown");
                    return;
                }

                var contest = competition.Contest;

                // Baseball plays don't carry a clock for tiebreak ordering. Use the
                // canonical CompetitionCompetitorScore record as the source of final
                // score: prefer SourceDescription = "Final", otherwise the most recent.
                var awayFinalScore = await _dataContext.CompetitionCompetitorScores
                    .AsNoTracking()
                    .Where(s => s.CompetitionCompetitorId == awayCompetitor.Id)
                    .OrderByDescending(s => s.SourceDescription == "Final" ? 1 : 0)
                    .ThenByDescending(s => s.CreatedUtc)
                    .Select(s => (double?)s.Value)
                    .FirstOrDefaultAsync();

                var homeFinalScore = await _dataContext.CompetitionCompetitorScores
                    .AsNoTracking()
                    .Where(s => s.CompetitionCompetitorId == homeCompetitor.Id)
                    .OrderByDescending(s => s.SourceDescription == "Final" ? 1 : 0)
                    .ThenByDescending(s => s.CreatedUtc)
                    .Select(s => (double?)s.Value)
                    .FirstOrDefaultAsync();

                if (awayFinalScore is null || homeFinalScore is null)
                {
                    _logger.LogInformation(
                        "No competitor scores available for {ContestName}. Data not ready for enrichment.",
                        contest.Name);
                    return;
                }

                contest.AwayScore = (int)awayFinalScore.Value;
                contest.HomeScore = (int)homeFinalScore.Value;

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

                if (competition.Odds?.Any() == true)
                {
                    EnrichOddsResults(
                        competition.Odds,
                        awayFranchiseSeasonId,
                        homeFranchiseSeasonId,
                        contest.AwayScore!.Value,
                        contest.HomeScore!.Value);

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
                odds.WinnerFranchiseSeasonId = null;
                odds.AtsWinnerFranchiseSeasonId = null;
                odds.OverUnderResult = OverUnderResult.None;

                if (awayScore != homeScore)
                {
                    odds.WinnerFranchiseSeasonId = homeScore > awayScore
                        ? homeFranchiseSeasonId
                        : awayFranchiseSeasonId;
                }

                if (odds.OverUnder.HasValue)
                {
                    odds.OverUnderResult = GetOverUnderResult(awayScore, homeScore, odds.OverUnder.Value);
                }

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
