using Microsoft.EntityFrameworkCore;
using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.Scoring
{
    public interface IPickScoringJob
    {
        Task ScoreAllAsync(CancellationToken cancellationToken = default);
    }

    public class PickScoringJob : IPickScoringJob
    {
        private readonly AppDataContext _db;
        private readonly IPickScoringService _scoringService;
        private readonly ILogger<PickScoringJob> _logger;

        public PickScoringJob(
            AppDataContext db,
            IPickScoringService scoringService,
            ILogger<PickScoringJob> logger)
        {
            _db = db;
            _scoringService = scoringService;
            _logger = logger;
        }

        public async Task ScoreAllAsync(CancellationToken cancellationToken = default)
        {
            var unscored = await _db.UserPicks
                .Where(p => p.IsCorrect == null)
                .ToListAsync(cancellationToken);

            if (unscored.Count == 0)
            {
                _logger.LogInformation("No unscored picks found.");
                return;
            }

            var contestIds = unscored.Select(p => p.ContestId).Distinct().ToList();
            var leagueIds = unscored.Select(p => p.PickemGroupId).Distinct().ToList();

            var contestList = await _db.Contests
                .Where(c => contestIds.Contains(c.ContestId))
                .ToListAsync(cancellationToken);

            var contests = contestList
                .Where(c => c.IsFinal)
                .ToDictionary(c => c.ContestId);

            var leagues = await _db.PickemGroups
                .Where(l => leagueIds.Contains(l.Id))
                .ToDictionaryAsync(l => l.Id, cancellationToken);

            var count = 0;

            foreach (var pick in unscored)
            {
                if (!contests.TryGetValue(pick.ContestId, out var contest) || !contest.IsFinal)
                    continue;

                if (!leagues.TryGetValue(pick.PickemGroupId, out var league))
                    continue;

                try
                {
                    _scoringService.ScorePick(pick, contest, league);
                    count++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to score pick {PickId}", pick.Id);
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Scored {Count} picks", count);
        }
    }
}
