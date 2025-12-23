using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Enums;
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

        private const string ProviderIdEspnBet = "58";
        private const string ProviderIdDraftKings = "100";

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
                    .Include(c => c.Odds.Where(o => o.ProviderId == "58" || o.ProviderId == "100"))
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

                var competitionExternalId = competition.ExternalIds
                    .FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);

                if (competitionExternalId == null)
                {
                    _logger.LogError("CompetitionExternalId not found. {@Command}", command);
                    return;
                }

                // Get the current status to ensure the game is actually over
                var statusUri = EspnUriMapper
                    .CompetitionRefToCompetitionStatusRef(new Uri(competitionExternalId.SourceUrl));

                var status = await _espnProvider.GetCompetitionStatusAsync(statusUri);
                if (status == null)
                {
                    _logger.LogError("Initial status fetch failed. {@Command}", command);
                    return;
                }

                //await _bus.Publish(new DocumentRequested(
                //    Id: HashProvider.GenerateHashFromUri(status.Ref),
                //    ParentId: contest.Id.ToString(),
                //    Uri: new Uri(competitionExternalId.SourceUrl),
                //    Sport: Sport.FootballNcaa,
                //    SeasonYear: competition.Contest.SeasonYear,
                //    DocumentType: DocumentType.EventCompetition,
                //    SourceDataProvider: SourceDataProvider.Espn,
                //    CorrelationId: command.CorrelationId,
                //    CausationId: CausationId.Producer.ContestEnrichmentProcessor
                //));
                //await _dataContext.SaveChangesAsync();

                var contest = competition.Contest;

                if (status.Type.Name != "STATUS_FINAL")
                {
                    _logger.LogWarning("Contest status is not yet final for {ContestName}. Found: {status}", contest.Name, status.Type.Name);
                    return;
                }

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
                    var awayRef = competition.Competitors
                        .First(cmp => cmp.HomeAway == "away").ExternalIds.First().SourceUrl;

                    var homeRef = competition.Competitors
                        .First(cmp => cmp.HomeAway == "home").ExternalIds.First().SourceUrl;

                    // source both
                    var awayComp = await _espnProvider.GetResource(new Uri(awayRef), true, true);
                    var homeComp = await _espnProvider.GetResource(new Uri(homeRef), true, true);

                    var awayCompDto = awayComp.FromJson<EspnEventCompetitionCompetitorDto>();
                    var homeCompDto = homeComp.FromJson<EspnEventCompetitionCompetitorDto>();

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
                    var awayScoreJson = await _espnProvider.GetResource(awayCompDto.Score.Ref);
                    var homeScoreJson = await _espnProvider.GetResource(homeCompDto.Score.Ref);

                    var awayScoreDto = awayScoreJson.FromJson<EspnEventCompetitionCompetitorScoreDto>();
                    var homeScoreDto = homeScoreJson.FromJson<EspnEventCompetitionCompetitorScoreDto>();

                    // update, persist, and exit
                    contest.AwayScore = (int)awayScoreDto!.Value;
                    contest.HomeScore = (int)homeScoreDto!.Value;

                    contest.FinalizedUtc = _dateTimeProvider.UtcNow();

                    await _bus.Publish(
                        new ContestEnrichmentCompleted(
                            command.ContestId,
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

                var awayFranchiseSeasonId = competition.Competitors
                    .First(cmp => cmp.HomeAway == "away").FranchiseSeasonId;
                var homeFranchiseSeasonId = competition.Competitors
                    .First(cmp => cmp.HomeAway == "home").FranchiseSeasonId;

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

                // were there odds on this game?
                var odds = competition.Odds?.Where(x => x.ProviderId == ProviderIdEspnBet).FirstOrDefault() ??
                           competition.Odds?.Where(x => x.ProviderId == ProviderIdDraftKings).FirstOrDefault();

                // TODO: Later we might want to score each odd individually - or even see if they were updated
                // they are indeed updated post-game.  Will verify.
                if (odds != null)
                {
                    if (odds.OverUnder.HasValue)
                    {
                        contest.OverUnder = GetOverUnderResult(
                            contest.AwayScore!.Value,
                            contest.HomeScore!.Value,
                            odds.OverUnder.Value);
                    }

                    if (odds.Spread.HasValue)
                    {
                        contest.SpreadWinnerFranchiseId = GetSpreadWinnerFranchiseSeasonId(
                            awayFranchiseSeasonId,
                            homeFranchiseSeasonId,
                            contest.AwayScore!.Value,
                            contest.HomeScore!.Value,
                            odds.Spread!.Value);
                    }
                }

                await _bus.Publish(
                    new ContestEnrichmentCompleted(
                        command.ContestId,
                        command.CorrelationId,
                        Guid.NewGuid()));
                await _dataContext.SaveChangesAsync();
            }
        }

        public OverUnderResult GetOverUnderResult(int awayScore, int homeScore, decimal overUnder)
        {
            var total = awayScore + homeScore;

            if (total > overUnder)
                return OverUnderResult.Over;

            if (total < overUnder)
                return OverUnderResult.Under;

            return OverUnderResult.Push;
        }

        public Guid? GetSpreadWinnerFranchiseSeasonId(
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
