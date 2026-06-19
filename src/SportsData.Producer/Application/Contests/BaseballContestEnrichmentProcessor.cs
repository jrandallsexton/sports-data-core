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
                       ["ContestId"] = command.ContestId,
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(EnrichContestCommand command)
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

            if (competition.Contest.FinalizedUtc != null)
            {
                _logger.LogInformation(
                    "Contest already finalized. Skipping. FinalizedUtc={FinalizedUtc}",
                    competition.Contest.FinalizedUtc);
                return;
            }

            var awayCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway == "away");
            var homeCompetitor = competition.Competitors.FirstOrDefault(c => c.HomeAway == "home");

            if (awayCompetitor is null || homeCompetitor is null)
            {
                _logger.LogError(
                    "Competition is missing away or home competitor. Away={HasAway}, Home={HasHome}",
                    awayCompetitor is not null, homeCompetitor is not null);
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

            // MAX(Value) per competitor. Cross-sport schema variance in
            // CompetitionCompetitorScores prevents a SourceDescription-based
            // filter (MLB inserts new "feed" rows alongside "basic/manual"
            // bootstrap rows; NCAAFB updates the "Basic/Manual" bootstrap
            // row in place — same SourceDescription before and after).
            // MAX side-steps it: bootstrap rows have Value=0 and any real
            // score exceeds them; in-game ticks only go up, so the highest
            // recorded value per competitor is always the latest known
            // cumulative score.
            var awayMaxScore = await _dataContext.CompetitionCompetitorScores
                .AsNoTracking()
                .Where(s => s.CompetitionCompetitorId == awayCompetitor.Id)
                .Select(s => (double?)s.Value)
                .MaxAsync();

            var homeMaxScore = await _dataContext.CompetitionCompetitorScores
                .AsNoTracking()
                .Where(s => s.CompetitionCompetitorId == homeCompetitor.Id)
                .Select(s => (double?)s.Value)
                .MaxAsync();

            if (awayMaxScore is null || homeMaxScore is null)
            {
                _logger.LogInformation(
                    "No competitor score rows for {ContestName}. Deferring to cron sweep.",
                    contest.Name);
                return;
            }

            // MLB games cannot end 0-0 in regulation — would proceed to extras
            // until a side leads. A 0-0 result here means only the bootstrap
            // rows exist (feed hasn't sourced yet) or genuinely-corrupt data;
            // defer rather than lock in a finalized 0-0 contest with null
            // Winner.
            if (awayMaxScore.Value == 0 && homeMaxScore.Value == 0)
            {
                _logger.LogWarning(
                    "MLB MAX competitor scores read as 0-0 for {ContestName} — implausible. Deferring enrichment.",
                    contest.Name);
                return;
            }

            contest.AwayScore = (int)awayMaxScore.Value;
            contest.HomeScore = (int)homeMaxScore.Value;

            var awayFranchiseSeasonId = awayCompetitor.FranchiseSeasonId;
            var homeFranchiseSeasonId = homeCompetitor.FranchiseSeasonId;

            if (contest.AwayScore != contest.HomeScore)
            {
                var homeWasWinner = contest.AwayScore < contest.HomeScore;

                contest.WinnerFranchiseSeasonId =
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
                    contest.SpreadWinnerFranchiseSeasonId = primaryOdds.AtsWinnerFranchiseSeasonId;
                }
            }

            await _bus.Publish(
                new ContestFinalized(
                    command.ContestId,
                    null,
                    contest.Sport,
                    contest.SeasonYear,
                    command.CorrelationId,
                    Guid.NewGuid()));
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Contest enrichment completed. ContestId={ContestId}, ContestName={ContestName}, " +
                "AwayScore={AwayScore}, HomeScore={HomeScore}, WinnerFranchiseSeasonId={WinnerFranchiseSeasonId}, " +
                "FinalizedUtc={FinalizedUtc}, OddsProvidersEnriched={OddsProvidersEnriched}",
                command.ContestId,
                contest.Name,
                contest.AwayScore,
                contest.HomeScore,
                contest.WinnerFranchiseSeasonId,
                contest.FinalizedUtc,
                competition.Odds?.Count(o => o.EnrichedUtc.HasValue) ?? 0);
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
