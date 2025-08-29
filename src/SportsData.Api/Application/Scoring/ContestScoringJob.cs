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

    public class ContestScoringJob : IScoreContests
    {
        private readonly ILogger<ContestScoringJob> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IProvideCanonicalData _canonicalData;
        public readonly IEventBus _bus;

        public ContestScoringJob(
            ILogger<ContestScoringJob> logger,
            AppDataContext dataContext,
            IProvideCanonicalData canonicalData,
            IEventBus bus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _canonicalData = canonicalData;
            _bus = bus;
        }

        public async Task Process(ScoreContestCommand command)
        {
            var result = await _canonicalData.GetMatchupResult(command.ContestId);

            if (result is null)
            {
                _logger.LogError("Result not found");
                return;
            }

            // now we need all UserPicks for this contest - including the group they are in
            var picks = await _dataContext.UserPicks
                .Where(p => p.ContestId == command.ContestId)
                .ToListAsync();

            foreach (var pick in picks)
            {
                var group = await _dataContext.PickemGroups
                    .Where(g => g.Id == pick.PickemGroupId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync();

                if (group is null)
                {
                    _logger.LogError("Group was null");
                    continue;
                }

                switch (group.PickType)
                {
                    case PickType.None:
                    case PickType.StraightUp:
                        if (!pick.FranchiseId.HasValue)
                        {
                            pick.IsCorrect = false;
                            pick.ScoredAt = DateTime.UtcNow;
                            pick.PointsAwarded = 0;
                        }
                        else
                        {
                            if (pick.FranchiseId == result.WinnerFranchiseSeasonId)
                            {
                                pick.IsCorrect = true;
                                pick.ScoredAt = DateTime.UtcNow;
                                pick.PointsAwarded = 1;
                            }
                        }
                        break;
                    case PickType.AgainstTheSpread:

                        if (!pick.FranchiseId.HasValue)
                        {
                            pick.IsCorrect = false;
                            pick.ScoredAt = DateTime.UtcNow;
                            pick.PointsAwarded = 0;
                        }
                        else
                        {
                            if (result.SpreadWinnerFranchiseSeasonId.HasValue)
                            {
                                if (pick.FranchiseId == result.SpreadWinnerFranchiseSeasonId.Value)
                                {
                                    pick.IsCorrect = true;
                                    pick.ScoredAt = DateTime.UtcNow;
                                    pick.PointsAwarded = 1;
                                }
                            }
                            else
                            {
                                // no spread. use straight up
                                if (pick.FranchiseId == result.WinnerFranchiseSeasonId)
                                {
                                    pick.IsCorrect = true;
                                    pick.ScoredAt = DateTime.UtcNow;
                                    pick.PointsAwarded = 1;
                                }
                            }
                        }
                        break;
                    case PickType.OverUnder:
                        continue;
                    default:
                        _logger.LogError("PickType was outside range");
                        continue;
                }

                await _dataContext.SaveChangesAsync();
            }
        }
    }

    public class ScoreContestCommand
    {
        public Guid ContestId { get; set; }

        public Guid CorrelationId { get; set; } = Guid.NewGuid();
    }
}
