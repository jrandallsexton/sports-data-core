using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
using SportsData.Core.Common;
using SportsData.Core.Eventing;

namespace SportsData.Api.Application.Scoring
{
    public interface IScoreContests
    {
        Task Process(ScoreContestCommand command);
    }

    public class ContestScoringProcessor : IScoreContests
    {
        private readonly ILogger<ContestScoringProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalData;
        private readonly IEventBus _bus;
        private readonly IPickScoringService _pickScoringService;
        private readonly ILeagueWeekScoringService _leagueWeekScoringService;

        public ContestScoringProcessor(
            ILogger<ContestScoringProcessor> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalData,
            IEventBus bus,
            IPickScoringService pickScoringService,
            ILeagueWeekScoringService leagueWeekScoringService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalData = canonicalData;
            _bus = bus;
            _pickScoringService = pickScoringService;
            _leagueWeekScoringService = leagueWeekScoringService;
        }

        public async Task Process(ScoreContestCommand command)
        {
            var result = await _canonicalData.GetMatchupResult(command.ContestId);
            
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
                    pick.ModifiedBy = CausationId.Api.ContestScoringProcessor;

                    await _dataContext.SaveChangesAsync();
                }

                // Track this league for week scoring using matchup data
                leaguesNeedingScoring.Add((group.Id, matchup.SeasonYear, matchup.SeasonWeek));
            }

            // After all picks are scored, trigger league week scoring
            foreach (var (leagueId, seasonYear, weekNumber) in leaguesNeedingScoring)
            {
                try
                {
                    _logger.LogInformation(
                        "Triggering league week scoring for leagueId={LeagueId}, seasonYear={SeasonYear}, week={Week} after contest scoring",
                        leagueId,
                        seasonYear,
                        weekNumber);

                    await _leagueWeekScoringService.ScoreLeagueWeekAsync(leagueId, seasonYear, weekNumber);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to score league week for leagueId={LeagueId}, seasonYear={SeasonYear}, week={Week}",
                        leagueId,
                        seasonYear,
                        weekNumber);
                    // Don't throw - we don't want to fail contest scoring if league scoring fails
                }
            }
        }
    }
}
