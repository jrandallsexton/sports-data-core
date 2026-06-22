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

    public class FootballContestEnrichmentProcessor : IEnrichContests
    {
        private readonly ILogger<FootballContestEnrichmentProcessor> _logger;
        private readonly FootballDataContext _dataContext;
        private readonly IEventBus _bus;
        private readonly IDateTimeProvider _dateTimeProvider;

        public FootballContestEnrichmentProcessor(
            ILogger<FootballContestEnrichmentProcessor> logger,
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
                       ["ContestId"] = command.ContestId,
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
                        "Competition is missing away or home competitor. ContestId={ContestId}, Away={HasAway}, Home={HasHome}",
                        command.ContestId, awayCompetitor is not null, homeCompetitor is not null);
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

                // Status was lifted off CompetitionBase onto the
                // sport-specific Football/Baseball subclasses. Loaded
                // independently via the abstract base set; EF
                // materializes whichever concrete subtype is registered
                // in the active per-sport context.
                var status = await _dataContext.CompetitionStatuses
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.CompetitionId == competition.Id);

                // If canonical status is not final, data isn't ready — return cleanly
                if (status?.StatusTypeName != "STATUS_FINAL")
                {
                    _logger.LogInformation(
                        "Contest status is not yet final for {ContestName}. Current: {Status}. Skipping enrichment.",
                        contest.Name, status?.StatusTypeName ?? "unknown");
                    return;
                }

                // Query canonical plays to determine final score (project to DTO)
                var lastScoringPlay = await _dataContext.CompetitionPlays
                    .AsNoTracking()
                    .Where(p => p.CompetitionId == competition.Id && p.ScoringPlay)
                    .OrderByDescending(p => p.PeriodNumber)
                    .ThenBy(p => p.ClockValue)
                    .Select(p => new { p.AwayScore, p.HomeScore })
                    .FirstOrDefaultAsync();

                if (lastScoringPlay != null)
                {
                    contest.AwayScore = lastScoringPlay.AwayScore;
                    contest.HomeScore = lastScoringPlay.HomeScore;
                }
                else
                {
                    // D2 fallback when no scoring plays exist. MAX(Value) per
                    // competitor — see the matching note in
                    // BaseballContestEnrichmentProcessor. Bootstrap-row schema
                    // differs across sports (NCAAFB updates "Basic/Manual"
                    // in place; MLB inserts a separate "feed" row), but in
                    // both cases MAX equals the latest known cumulative score.
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
                            "No scoring plays and no competitor score rows for {ContestName}. Deferring to cron sweep.",
                            contest.Name);
                        return;
                    }

                    // D2 path + 0-0 MAX = no scoring plays AND only bootstrap
                    // rows exist. NFL hasn't had a 0-0 final since 1943 and
                    // NCAA OT rules guarantee a non-tie; this state is
                    // always stale data, never a legitimate final.
                    if (awayMaxScore.Value == 0 && homeMaxScore.Value == 0)
                    {
                        _logger.LogWarning(
                            "D2 MAX competitor scores read as 0-0 for {ContestName} (no scoring plays either) — implausible. Deferring enrichment.",
                            contest.Name);
                        return;
                    }

                    contest.AwayScore = (int)awayMaxScore.Value;
                    contest.HomeScore = (int)homeMaxScore.Value;
                }

                _logger.LogInformation(
                    "Final score derived. Source={Source}, Away={AwayScore}, Home={HomeScore}",
                    lastScoringPlay != null ? "ScoringPlay" : "CompetitorMaxScore",
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
