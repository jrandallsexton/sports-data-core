using Hangfire;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.Scoring;

public interface IScoreLeagueWeeks
{
    // The DCE attribute MUST live here, not on the implementing class:
    // callers enqueue via Enqueue<IScoreLeagueWeeks>(...), so Hangfire
    // stores the job against this interface method and resolves job
    // filters from it — attributes on the implementation are never seen.
    // Prod proof (2026-08-28): four simultaneous finalizations ran twelve
    // concurrent Process invocations and revived the 23505 race the
    // class-level attribute was meant to kill.
    [DisableConcurrentExecution(60)]
    Task Process(Guid leagueId, int seasonYear, int seasonWeek, Guid correlationId);
}

/// <summary>
/// Per-(league, year, week) Hangfire job. Replaces the inline tail call inside
/// <see cref="PickScoringProcessor"/> so a burst of contests finalizing in
/// the same scoring week cannot fan out into N concurrent
/// <see cref="ILeagueWeekScoringService.ScoreLeagueWeekAsync"/> invocations
/// racing on the <c>PickemGroupWeekResult</c> unique index.
///
/// Two-layer safety:
///   1. <see cref="DisableConcurrentExecutionAttribute"/> on
///      <see cref="IScoreLeagueWeeks.Process"/> serializes invocations
///      across the cluster. Best-effort: a broken storage connection can
///      release the lock without notice, so this reduces the probability
///      of the 23505 race rather than eliminating it — the check-then-
///      insert in ScoreLeagueWeekAsync plus Hangfire retry (which finds
///      the existing row and updates) remain the backstop.
///   2. A staleness short-circuit collapses N queued runs into 1 actual rescore:
///      the first run does the work; the rest find fresh state and exit.
///
/// Trade-off: DCE serializes ALL league-week scoring globally, not per tuple.
/// Acceptable while per-run work is sub-second; revisit with a per-tuple
/// server filter or pg_advisory lock if contention grows.
/// </summary>
public class LeagueWeekScoringProcessor : IScoreLeagueWeeks
{
    private readonly ILogger<LeagueWeekScoringProcessor> _logger;
    private readonly AppDataContext _dataContext;
    private readonly ILeagueWeekScoringService _leagueWeekScoringService;

    public LeagueWeekScoringProcessor(
        ILogger<LeagueWeekScoringProcessor> logger,
        AppDataContext dataContext,
        ILeagueWeekScoringService leagueWeekScoringService)
    {
        _logger = logger;
        _dataContext = dataContext;
        _leagueWeekScoringService = leagueWeekScoringService;
    }

    public async Task Process(Guid leagueId, int seasonYear, int seasonWeek, Guid correlationId)
    {
        _logger.LogInformation(
            "Starting league-week scoring. LeagueId={LeagueId}, SeasonYear={SeasonYear}, Week={Week}, CorrelationId={CorrelationId}",
            leagueId, seasonYear, seasonWeek, correlationId);

        // Staleness short-circuit: skip if no UserPick.ScoredAt is newer than
        // the OLDEST PickemGroupWeekResult.CalculatedUtc for this tuple. Min
        // (not Max) so a brand-new member's freshly-inserted row can't mask a
        // stale row for someone else. Mirrors the predicate the daily backstop
        // (LeagueWeekScoringJob) uses.
        var oldestCalc = await _dataContext.PickemGroupWeekResults
            .Where(r => r.PickemGroupId == leagueId
                        && r.SeasonYear == seasonYear
                        && r.SeasonWeek == seasonWeek)
            .MinAsync(r => (DateTime?)r.CalculatedUtc);

        if (oldestCalc.HasValue)
        {
            var latestPickScored = await _dataContext.PickemGroupMatchups
                .Where(m => m.GroupId == leagueId
                            && m.SeasonYear == seasonYear
                            && m.SeasonWeek == seasonWeek)
                .Join(_dataContext.UserPicks.Where(p => p.ScoredAt != null && p.PickemGroupId == leagueId),
                    m => m.ContestId,
                    p => p.ContestId,
                    (m, p) => p.ScoredAt)
                .MaxAsync(s => (DateTime?)s);

            if (!latestPickScored.HasValue || latestPickScored.Value <= oldestCalc.Value)
            {
                _logger.LogInformation(
                    "League-week scoring already current; skipping. LeagueId={LeagueId}, SeasonYear={SeasonYear}, Week={Week}, OldestCalc={OldestCalc}, LatestPickScored={LatestPickScored}, CorrelationId={CorrelationId}",
                    leagueId, seasonYear, seasonWeek, oldestCalc, latestPickScored, correlationId);
                return;
            }
        }

        await _leagueWeekScoringService.ScoreLeagueWeekAsync(leagueId, seasonYear, seasonWeek);
    }
}
