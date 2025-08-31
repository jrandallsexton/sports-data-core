using Microsoft.EntityFrameworkCore;

using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Canonical;
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

            if (result is null)
            {
                _logger.LogError("Result not found");
                return;
            }

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
                    .Where(g => g.Id == kvp.Key)
                    .AsNoTracking()
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
                        _pickScoringService.ScorePick(group, pick, result);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error scoring pick {PickId} for group {GroupId}", pick.Id, group.Id);
                    }

                    await _dataContext.SaveChangesAsync();
                }

            }
        }
    }
}
