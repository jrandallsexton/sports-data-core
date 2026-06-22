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

            var contestAlreadyFinalized = competition.Contest.FinalizedUtc != null;
            var unfinalizedOdds = competition.Odds?
                .Where(o => o.FinalizedUtc == null).ToList() ?? new List<CompetitionOdds>();

            if (contestAlreadyFinalized && unfinalizedOdds.Count == 0)
            {
                _logger.LogInformation(
                    "Contest and all odds rows already finalized. Nothing to do. FinalizedUtc={FinalizedUtc}",
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

            var contest = competition.Contest;

            if (contestAlreadyFinalized)
            {
                // Odds-late path: Contest finalized earlier, but ≥1
                // CompetitionOdds row arrived (or stayed unfinalized) after.
                // Reuse the persisted Contest scores — do NOT re-derive
                // from CompetitorScores so we don't drift from the values
                // picks were scored against. Do NOT republish
                // ContestFinalized — downstream picks scoring already ran;
                // republishing would double-score.
                if (contest.AwayScore is null || contest.HomeScore is null)
                {
                    _logger.LogWarning(
                        "Contest finalized but missing AwayScore/HomeScore — cannot finalize {UnfinalizedOddsCount} late odds row(s). Manual intervention required.",
                        unfinalizedOdds.Count);
                    return;
                }

                _logger.LogInformation(
                    "Contest already finalized; running odds-late finalization for {UnfinalizedOddsCount} provider(s).",
                    unfinalizedOdds.Count);

                EnrichOddsResults(
                    unfinalizedOdds,
                    awayCompetitor.FranchiseSeasonId,
                    homeCompetitor.FranchiseSeasonId,
                    contest.AwayScore.Value,
                    contest.HomeScore.Value);

                // Refresh Contest-level denorm if a higher-priority provider
                // just landed (e.g. EspnBet missing before, now present —
                // promote it).
                var primaryOddsLate = competition.Odds!
                    .FirstOrDefault(o => o.FinalizedUtc.HasValue && o.ProviderId == SportsBook.EspnBet.ToProviderId())
                    ?? competition.Odds!.FirstOrDefault(o => o.FinalizedUtc.HasValue);

                if (primaryOddsLate != null)
                {
                    var denormChanged =
                        contest.OverUnder != primaryOddsLate.OverUnderResult
                        || contest.SpreadWinnerFranchiseSeasonId != primaryOddsLate.AtsWinnerFranchiseSeasonId;

                    contest.OverUnder = primaryOddsLate.OverUnderResult;
                    contest.SpreadWinnerFranchiseSeasonId = primaryOddsLate.AtsWinnerFranchiseSeasonId;

                    if (denormChanged)
                    {
                        _logger.LogWarning(
                            "Contest denorm refreshed by late primary odds. ProviderName={ProviderName}, OverUnderResult={OverUnderResult}, AtsWinner={AtsWinnerFranchiseSeasonId}. Downstream picks NOT re-scored (per-provider scoring is a future refactor).",
                            primaryOddsLate.ProviderName,
                            primaryOddsLate.OverUnderResult,
                            primaryOddsLate.AtsWinnerFranchiseSeasonId);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "Primary odds provider unchanged after odds-late finalization. ProviderName={ProviderName}",
                            primaryOddsLate.ProviderName);
                    }
                }

                await _dataContext.SaveChangesAsync();

                _logger.LogInformation(
                    "Odds-late finalization completed. OddsProvidersFinalized={OddsProvidersFinalized}",
                    competition.Odds!.Count(o => o.FinalizedUtc.HasValue));
                return;
            }

            var status = await _dataContext.CompetitionStatuses
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.CompetitionId == competition.Id);

            if (status?.StatusTypeName != "STATUS_FINAL")
            {
                _logger.LogInformation(
                    "Contest status is not yet final for {ContestName}. Current: {Status}. Skipping enrichment.",
                    contest.Name, status?.StatusTypeName ?? "unknown");
                return;
            }

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

            // MLB games cannot end tied in regulation — extras run until a
            // side leads at the end of an inning. A tied result here means
            // ESPN feed sourcing hasn't caught the deciding inning yet, or
            // the row is genuinely corrupt. Defer rather than lock in a
            // finalized tied contest with null Winner (the
            // contest.AwayScore != contest.HomeScore branch below would be
            // skipped, leaving WinnerFranchiseSeasonId unset). The
            // statistically-negligible "officially-tied" cases (rain-
            // shortened tie game, the historical 2002 All-Star Game) get
            // deferred forever and surface via warning logs — acceptable
            // cost vs. corrupting picks scoring for an entire league.
            if (awayMaxScore.Value == homeMaxScore.Value)
            {
                _logger.LogWarning(
                    "MLB MAX competitor scores are tied ({Score}-{Score}) for {ContestName} — implausible final. Deferring enrichment.",
                    awayMaxScore.Value, homeMaxScore.Value, contest.Name);
                return;
            }

            contest.AwayScore = (int)awayMaxScore.Value;
            contest.HomeScore = (int)homeMaxScore.Value;

            _logger.LogInformation(
                "Final score derived. Source={Source}, Away={AwayScore}, Home={HomeScore}",
                "CompetitorMaxScore",
                contest.AwayScore,
                contest.HomeScore);

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

            _logger.LogInformation(
                "Straight-up winner derived. WinnerFranchiseSeasonId={WinnerFranchiseSeasonId}, IsTie={IsTie}",
                contest.WinnerFranchiseSeasonId,
                contest.AwayScore == contest.HomeScore);

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
                    .FirstOrDefault(o => o.FinalizedUtc.HasValue && o.ProviderId == SportsBook.EspnBet.ToProviderId())
                    ?? competition.Odds.FirstOrDefault(o => o.FinalizedUtc.HasValue);

                if (primaryOdds != null)
                {
                    contest.OverUnder = primaryOdds.OverUnderResult;
                    contest.SpreadWinnerFranchiseSeasonId = primaryOdds.AtsWinnerFranchiseSeasonId;

                    _logger.LogInformation(
                        "Primary odds provider selected. ProviderName={ProviderName}, OverUnderResult={OverUnderResult}, AtsWinner={AtsWinnerFranchiseSeasonId}",
                        primaryOdds.ProviderName,
                        primaryOdds.OverUnderResult,
                        primaryOdds.AtsWinnerFranchiseSeasonId);
                }
                else
                {
                    _logger.LogInformation(
                        "No finalized odds row available; Contest-level ATS / OverUnder not denormalized.");
                }
            }
            else
            {
                _logger.LogInformation(
                    "No CompetitionOdds rows present; skipping per-provider enrichment.");
            }

            await _bus.Publish(
                new ContestFinalized(
                    ContestId: command.ContestId,
                    Ref: null,
                    Sport: contest.Sport,
                    SeasonYear: contest.SeasonYear,
                    CorrelationId: command.CorrelationId,
                    CausationId: Guid.NewGuid(),
                    AwayScore: contest.AwayScore,
                    HomeScore: contest.HomeScore,
                    WinnerFranchiseSeasonId: contest.WinnerFranchiseSeasonId,
                    SpreadWinnerFranchiseSeasonId: contest.SpreadWinnerFranchiseSeasonId,
                    OverUnderResultRaw: (int)contest.OverUnder,
                    CompletedUtc: contest.FinalizedUtc));
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Contest enrichment completed. ContestName={ContestName}, " +
                "AwayScore={AwayScore}, HomeScore={HomeScore}, WinnerFranchiseSeasonId={WinnerFranchiseSeasonId}, " +
                "SpreadWinnerFranchiseSeasonId={SpreadWinnerFranchiseSeasonId}, OverUnder={OverUnder}, " +
                "FinalizedUtc={FinalizedUtc}, OddsProvidersFinalized={OddsProvidersFinalized}",
                contest.Name,
                contest.AwayScore,
                contest.HomeScore,
                contest.WinnerFranchiseSeasonId,
                contest.SpreadWinnerFranchiseSeasonId,
                contest.OverUnder,
                contest.FinalizedUtc,
                competition.Odds?.Count(o => o.FinalizedUtc.HasValue) ?? 0);
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

                odds.FinalizedUtc = _dateTimeProvider.UtcNow();

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
