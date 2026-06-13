using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Scoring;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common.Jobs;

namespace SportsData.Api.Application.Jobs
{
    /// <summary>
    /// Daily backstop for the leaderboard-scoring path.
    ///
    /// Primary trigger is the tail call inside
    /// <see cref="PickScoringProcessor"/>: after picks for a contest are
    /// scored, the processor invokes
    /// <see cref="ILeagueWeekScoringService.ScoreLeagueWeekAsync"/> for each
    /// (league, year, week) tuple that had picks in that contest. This job
    /// catches league weeks where the tail call failed (e.g., a transient
    /// exception or pod restart between contest scoring and league-week
    /// scoring).
    ///
    /// Sport-agnostic by construction: the staleness predicate only looks at
    /// <c>UserPick.ScoredAt</c> and <c>PickemGroupWeekResult.CalculatedUtc</c>
    /// — no sport-specific endpoints or week semantics. <see cref="ILeagueWeekScoringService"/>
    /// itself is already sport-neutral.
    /// </summary>
    public class LeagueWeekScoringJob : IAmARecurringJob
    {
        private readonly ILogger<LeagueWeekScoringJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ILeagueWeekScoringService _leagueWeekScoringService;

        public LeagueWeekScoringJob(
            ILogger<LeagueWeekScoringJob> logger,
            AppDataContext dataContext,
            ILeagueWeekScoringService leagueWeekScoringService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _leagueWeekScoringService = leagueWeekScoringService;
        }

        public async Task ExecuteAsync()
        {
            _logger.LogInformation("Starting {JobName}", nameof(LeagueWeekScoringJob));

            try
            {
                // For each league week with any scored picks, find the most-recently-
                // scored pick. Pairs (league, year, week) → max UserPick.ScoredAt.
                var pickStatusByLeagueWeek = await _dataContext.PickemGroupMatchups
                    .Join(_dataContext.UserPicks.Where(p => p.ScoredAt != null),
                        m => new { m.ContestId, GroupId = m.GroupId },
                        p => new { p.ContestId, GroupId = p.PickemGroupId },
                        (m, p) => new { GroupId = m.GroupId, m.SeasonYear, m.SeasonWeek, p.ScoredAt })
                    .GroupBy(x => new { x.GroupId, x.SeasonYear, x.SeasonWeek })
                    .Select(g => new
                    {
                        g.Key.GroupId,
                        g.Key.SeasonYear,
                        g.Key.SeasonWeek,
                        LatestPickScored = g.Max(x => x.ScoredAt)
                    })
                    .ToListAsync();

                // For each league week with any result row, find the OLDEST
                // calculation timestamp across all user rows.
                //
                // PickemGroupWeekResult is one row per user per league-week. Using
                // Min rather than Max means the league-week is only considered
                // fresh when every member's row is up to date. If we used Max, a
                // single recently-updated row (e.g. a new league member whose row
                // was just created) would mask older rows for other members and
                // leave them stale.
                var resultStatusByLeagueWeek = await _dataContext.PickemGroupWeekResults
                    .GroupBy(r => new { r.PickemGroupId, r.SeasonYear, r.SeasonWeek })
                    .Select(g => new
                    {
                        GroupId = g.Key.PickemGroupId,
                        g.Key.SeasonYear,
                        g.Key.SeasonWeek,
                        OldestCalc = g.Min(r => (DateTime?)r.CalculatedUtc)
                    })
                    .ToListAsync();

                var resultLookup = resultStatusByLeagueWeek.ToDictionary(
                    r => (r.GroupId, r.SeasonYear, r.SeasonWeek),
                    r => r.OldestCalc);

                // Stale = pick scored more recently than the oldest user's last
                // calculation, OR no result row exists yet.
                var staleLeagueWeeks = pickStatusByLeagueWeek
                    .Where(p =>
                    {
                        var oldestCalc = resultLookup.GetValueOrDefault((p.GroupId, p.SeasonYear, p.SeasonWeek));
                        return oldestCalc == null
                            || (p.LatestPickScored.HasValue && p.LatestPickScored > oldestCalc);
                    })
                    .ToList();

                _logger.LogInformation(
                    "Found {Count} stale league weeks needing leaderboard rescore.",
                    staleLeagueWeeks.Count);

                var successCount = 0;
                var failureCount = 0;

                foreach (var lw in staleLeagueWeeks)
                {
                    try
                    {
                        _logger.LogInformation(
                            "Rescoring league week: leagueId={LeagueId}, year={Year}, week={Week}",
                            lw.GroupId, lw.SeasonYear, lw.SeasonWeek);

                        await _leagueWeekScoringService.ScoreLeagueWeekAsync(
                            lw.GroupId, lw.SeasonYear, lw.SeasonWeek);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Error rescoring league week: leagueId={LeagueId}, year={Year}, week={Week}",
                            lw.GroupId, lw.SeasonYear, lw.SeasonWeek);
                        failureCount++;
                    }
                }

                _logger.LogInformation(
                    "Completed {JobName}: {Success} successful, {Failure} failed",
                    nameof(LeagueWeekScoringJob), successCount, failureCount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in {JobName}", nameof(LeagueWeekScoringJob));
                throw;
            }
        }
    }
}
