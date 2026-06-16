using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Infrastructure.Clients.Contest;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Processing;

namespace SportsData.Api.Application.Scoring
{
    public interface IScorePicks
    {
        Task Process(ScorePicksCommand command);
    }

    public class PickScoringProcessor : IScorePicks
    {
        private readonly ILogger<PickScoringProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IContestClientFactory _contestClientFactory;
        private readonly IEventBus _bus;
        private readonly IPickScoringService _pickScoringService;
        private readonly IProvideBackgroundJobs _backgroundJobProvider;

        public PickScoringProcessor(
            ILogger<PickScoringProcessor> logger,
            AppDataContext dataContext,
            IContestClientFactory contestClientFactory,
            IEventBus bus,
            IPickScoringService pickScoringService,
            IProvideBackgroundJobs backgroundJobProvider)
        {
            _logger = logger;
            _dataContext = dataContext;
            _contestClientFactory = contestClientFactory;
            _bus = bus;
            _pickScoringService = pickScoringService;
            _backgroundJobProvider = backgroundJobProvider;
        }

        public async Task Process(ScorePicksCommand command)
        {
            // Short-circuit: ContestFinalized may re-deliver (at-least-once delivery)
            // and the cron backstop may enqueue contests whose picks have already
            // been scored. Skip the Producer round-trip and per-pick iteration when
            // there is nothing left to do.
            var hasUnscoredPicks = await _dataContext.UserPicks
                .AnyAsync(p => p.ContestId == command.ContestId && p.ScoredAt == null);

            if (!hasUnscoredPicks)
            {
                _logger.LogInformation(
                    "Contest already scored or no picks exist. Skipping. ContestId={ContestId}",
                    command.ContestId);
                return;
            }

            // Resolve sport for this contest by joining PickemGroupMatchups to
            // PickemGroups. A contest belongs to exactly one sport, so any matchup
            // row that references this contest carries the canonical Sport via its
            // PickemGroup. The (Sport?) projection disambiguates "no row" from
            // "row with Sport.All (== 0)".
            var sport = await _dataContext.PickemGroupMatchups
                .Where(m => m.ContestId == command.ContestId)
                .Join(_dataContext.PickemGroups,
                    m => m.GroupId,
                    g => g.Id,
                    (m, g) => (Sport?)g.Sport)
                .FirstOrDefaultAsync();

            if (sport is null)
            {
                _logger.LogWarning(
                    "Could not resolve sport for contest. No PickemGroupMatchup found. ContestId={ContestId}",
                    command.ContestId);
                return;
            }

            var matchupResultResponse = await _contestClientFactory
                .Resolve(sport.Value)
                .GetMatchupResult(command.ContestId);

            if (!matchupResultResponse.IsSuccess)
            {
                if (matchupResultResponse.Status == ResultStatus.NotFound)
                {
                    _logger.LogWarning("Matchup result not found for contest {ContestId}. Skipping scoring.", command.ContestId);
                    return;
                }

                _logger.LogError("Failed to retrieve matchup result for contest {ContestId}. Status={Status}", command.ContestId, matchupResultResponse.Status);
                throw new InvalidOperationException($"Failed to retrieve matchup result for contest {command.ContestId}");
            }

            var result = matchupResultResponse.Value;

            // Belt-and-suspenders: GetMatchupResultByContestId.sql already
            // filters to FinalizedUtc IS NOT NULL, so a finalized=null result
            // here means either (a) the SQL filter was reverted, or (b) some
            // future caller path bypasses the producer handler. Refuse to
            // score either way — the cron backstop will retry once enrichment
            // lands. See docs/contest-finalization-event-restructure.md and
            // the 2026-06-16 bug where the cron pulled unfinalized contests
            // and silently scored picks against Guid.Empty.
            if (result.FinalizedUtc is null)
            {
                _logger.LogWarning(
                    "Matchup result has no FinalizedUtc — contest is not yet enriched. Skipping scoring. ContestId={ContestId}",
                    command.ContestId);
                return;
            }

            // canonical data has the true spread winner, but that is based on the final spread
            // our matchups were generated with the opening spread, so we need to adjust
            // we cannot score picks based on the final spread
            // instead, we need to determine the spread winner based on the snapshot
            // of the spread at the time we generated the matchup

            // we need all UserPicks for this contest - including the group they are in
            var picks = await _dataContext.UserPicks
                .Include(p => p.Group)
                .Where(p => p.ContestId == command.ContestId)                
                .ToListAsync();

            var dictionary = picks
                .GroupBy(p => p.PickemGroupId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(p => p).ToList()
                );

            // Track which leagues need week scoring
            var leaguesNeedingScoring = new HashSet<(Guid LeagueId, int SeasonYear, int WeekNumber)>();

            foreach (var kvp in dictionary)
            {
                var group = await _dataContext.PickemGroups
                    .Include(g => g.Weeks.Where(x => x.SeasonWeekId == result.SeasonWeekId))
                    .ThenInclude(w => w.Matchups.Where(m => m.ContestId == result.ContestId))
                    .Where(g => g.Id == kvp.Key)
                    .AsNoTracking()
                    .AsSplitQuery()
                    .FirstOrDefaultAsync();

                if (group is null)
                {
                    _logger.LogError("Group was null");
                    continue;
                }

                // Get the matchup to extract season year and week number
                var matchup = group.Weeks.FirstOrDefault()?.Matchups.FirstOrDefault();
                if (matchup == null)
                {
                    _logger.LogWarning(
                        "Could not find matchup for contestId={ContestId} in groupId={GroupId}",
                        command.ContestId,
                        group.Id);
                    continue;
                }

                foreach (var pick in kvp.Value)
                {
                    try
                    {
                        // TODO: Make this a league option (LockSpreadAtPick, DoNotLockSpreadPicks)
                        //_pickScoringService.ScorePick(
                        //    group,
                        //    group.Weeks.First().Matchups.First().HomeSpread,
                        //    pick,
                        //    result);

                        _pickScoringService.ScorePick(
                            group,
                            result.Spread,
                            pick,
                            result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error scoring pick {PickId} for group {GroupId}", pick.Id, group.Id);
                    }

                    pick.ModifiedUtc = DateTime.UtcNow;
                    pick.ModifiedBy = CausationId.Api.PickScoringProcessor;

                    await _dataContext.SaveChangesAsync();
                }

                // Track this league for week scoring using matchup data
                leaguesNeedingScoring.Add((group.Id, matchup.SeasonYear, matchup.SeasonWeek));
            }

            // Enqueue per (league, year, week) instead of calling inline. The
            // IScoreLeagueWeeks job is DisableConcurrentExecution-decorated and
            // runs a staleness short-circuit, so a burst of contests finalizing
            // in the same scoring week (a) cannot race on the
            // PickemGroupWeekResult unique index and (b) collapses into a
            // single real rescore rather than N.
            foreach (var (leagueId, seasonYear, weekNumber) in leaguesNeedingScoring)
            {
                try
                {
                    _backgroundJobProvider.Enqueue<IScoreLeagueWeeks>(
                        p => p.Process(leagueId, seasonYear, weekNumber, command.CorrelationId));

                    _logger.LogInformation(
                        "Enqueued league-week scoring. LeagueId={LeagueId}, SeasonYear={SeasonYear}, Week={Week}, CorrelationId={CorrelationId}",
                        leagueId,
                        seasonYear,
                        weekNumber,
                        command.CorrelationId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to enqueue league-week scoring. LeagueId={LeagueId}, SeasonYear={SeasonYear}, Week={Week}",
                        leagueId,
                        seasonYear,
                        weekNumber);
                    // Don't throw — the recurring LeagueWeekScoringJob backstop
                    // catches league weeks where the tail enqueue failed.
                }
            }
        }
    }
}
