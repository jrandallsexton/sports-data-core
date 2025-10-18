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

        public ContestScoringProcessor(
            ILogger<ContestScoringProcessor> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalData,
            IEventBus bus,
            IPickScoringService pickScoringService)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalData = canonicalData;
            _bus = bus;
            _pickScoringService = pickScoringService;
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

            }
        }
    }
}
