using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
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
        private readonly IProvideEspnApiData _espnProvider;
        private readonly IEventBus _bus;
        private readonly IDateTimeProvider _dateTimeProvider;

        public ContestEnrichmentProcessor(
            ILogger<ContestEnrichmentProcessor> logger,
            FootballDataContext dataContext,
            IProvideEspnApiData espnProvider,
            IEventBus bus,
            IDateTimeProvider dateTimeProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _espnProvider = espnProvider;
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
                    .Include(c => c.ExternalIds)
                    .Include(c => c.Competitors)
                    .ThenInclude(comp => comp.ExternalIds)
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

                var competitionExternalId = competition.ExternalIds
                    .FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);

                if (competitionExternalId == null)
                {
                    _logger.LogError("CompetitionExternalId not found. {@Command}", command);
                    return;
                }

                // verify completion
                var compStatus = competition.Status;

                if (compStatus?.StatusTypeName != "STATUS_FINAL")
                {
                    // get from ESPN?
                    // Get the current status to ensure the game is actually over
                    var statusUri = EspnUriMapper
                        .CompetitionRefToCompetitionStatusRef(new Uri(competitionExternalId.SourceUrl));

                    var status = await _espnProvider.GetCompetitionStatusAsync(statusUri);
                    if (status == null)
                    {
                        _logger.LogError("Initial status fetch failed. {@Command}", command);
                        return;
                    }

                    if (status.Type.Name != "STATUS_FINAL")
                    {
                        _logger.LogWarning("Contest status is not yet final for {ContestName}. Found: {status}", competition.Contest.Name, status.Type.Name);
                        return;
                    }
                }

                var contest = competition.Contest;

                // in order to calculate the final score and winners, we need to get all plays
                // take the last scoring play and that is what we have
                var playsUri = EspnUriMapper
                    .CompetitionRefToCompetitionPlaysRef(new Uri(competitionExternalId.SourceUrl), 999);

                var plays = await _espnProvider.GetCompetitionPlaysAsync(playsUri);
                if (plays == null)
                {
                    _logger.LogError("Fetching plays failed. {@Command}", command);
                    return;
                }

                if (plays.Count == 0)
                {
                    _logger.LogWarning("No plays found for {ContestName}", contest.Name);

                    // this is very likely a D2 game.  try to get it from Competition.Competitor[x].Score.Ref
                    var awayExternalId = awayCompetitor.ExternalIds.FirstOrDefault();
                    var homeExternalId = homeCompetitor.ExternalIds.FirstOrDefault();

                    if (awayExternalId is null || homeExternalId is null)
                    {
                        _logger.LogError(
                            "Competitor ExternalIds missing for D2 fallback. ContestId={ContestId}, AwayHasExtId={AwayHasExtId}, HomeHasExtId={HomeHasExtId}",
                            command.ContestId, awayExternalId is not null, homeExternalId is not null);
                        return;
                    }

                    var awayRef = awayExternalId.SourceUrl;
                    var homeRef = homeExternalId.SourceUrl;

                    // source both
                    var awayCompResult = await _espnProvider.GetResource(new Uri(awayRef), true, true);
                    var homeCompResult = await _espnProvider.GetResource(new Uri(homeRef), true, true);
                    
                    if (!awayCompResult.IsSuccess || !homeCompResult.IsSuccess)
                    {
                        _logger.LogError("Failed to fetch competitor data from ESPN");
                        return;
                    }

                    var awayCompDto = awayCompResult.Value.FromJson<EspnEventCompetitionCompetitorDto>();
                    var homeCompDto = homeCompResult.Value.FromJson<EspnEventCompetitionCompetitorDto>();

                    if (awayCompDto is null)
                    {
                        _logger.LogError("Away competitor could not be deserialized");
                        return;
                    }

                    if (homeCompDto is null)
                    {
                        _logger.LogError("Home competitor could not be deserialized");
                        return;
                    }

                    // get the score for both
                    var awayScoreResult = await _espnProvider.GetResource(awayCompDto.Score.Ref);
                    var homeScoreResult = await _espnProvider.GetResource(homeCompDto.Score.Ref);
                    
                    if (!awayScoreResult.IsSuccess || !homeScoreResult.IsSuccess)
                    {
                        _logger.LogError("Failed to fetch score data from ESPN");
                        return;
                    }

                    var awayScoreDto = awayScoreResult.Value.FromJson<EspnEventCompetitionCompetitorScoreDto>();
                    var homeScoreDto = homeScoreResult.Value.FromJson<EspnEventCompetitionCompetitorScoreDto>();

                    // update, persist, and exit
                    if (awayScoreDto is not null)
                        contest.AwayScore = (int)awayScoreDto!.Value;

                    if (homeScoreDto is not null)
                        contest.HomeScore = (int)homeScoreDto!.Value;

                    contest.FinalizedUtc = _dateTimeProvider.UtcNow();

                    await _bus.Publish(
                        new ContestEnrichmentCompleted(
                            command.ContestId,
                            null,
                            contest.Sport,
                            contest.SeasonYear,
                            command.CorrelationId,
                            Guid.NewGuid()));
                    await _dataContext.SaveChangesAsync();

                    return;
                }

                var finalScoringPlay = plays?.Items?
                    .Where(x => x.ScoringPlay)
                    .TakeLast(1)
                    .FirstOrDefault();

                if (finalScoringPlay == null)
                {
                    _logger.LogWarning("No scoring plays found.  Assume zero?");
                    contest.AwayScore = 0;
                    contest.HomeScore = 0;
                }
                else
                {
                    contest.AwayScore = finalScoringPlay.AwayScore;
                    contest.HomeScore = finalScoringPlay.HomeScore;
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
                contest.EndDateUtc = plays?.Items?.Last().Wallclock;

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
                        Sport.FootballNcaa,
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
