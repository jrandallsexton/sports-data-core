using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leaderboard.Dtos;
using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.UI.Leaderboard
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardUserDto>> GetLeaderboardAsync(
            Guid groupId,
            int currentWeek,
            CancellationToken cancellationToken);
    }

    public class LeaderboardService : ILeaderboardService
    {
        private readonly AppDataContext _dataContext;

        public LeaderboardService(AppDataContext dataContext)
        {
            _dataContext = dataContext;
        }

        public async Task<List<LeaderboardUserDto>> GetLeaderboardAsync(
            Guid groupId,
            int currentWeek,
            CancellationToken cancellationToken)
        {
            var leaderboard = await _dataContext.UserPicks
                .Where(p => p.PickemGroupId == groupId && p.PointsAwarded != null)
                .GroupBy(p => new { p.UserId, p.User.DisplayName })
                .Select(g => new
                {
                    g.Key.UserId,
                    g.Key.DisplayName,
                    TotalPoints = g.Sum(p => p.PointsAwarded ?? 0),
                    CurrentWeekPoints = g
                        .Where(p => p.Week == currentWeek)
                        .Sum(p => p.PointsAwarded ?? 0),
                    WeeksPlayed = g.Select(p => p.Week).Distinct().Count(),
                    TotalPicks = g.Count(),
                    TotalCorrect = g.Count(p => p.IsCorrect.HasValue && p.IsCorrect.Value == true)
                })
                .ToListAsync(cancellationToken);

            var result = new List<LeaderboardUserDto>();

            int rank = 1;
            int position = 0;
            int? previousPoints = null;

            foreach (var x in leaderboard.OrderByDescending(x => x.TotalPoints))
            {
                position++;

                if (previousPoints != x.TotalPoints)
                {
                    rank = position;
                }

                result.Add(new LeaderboardUserDto
                {
                    UserId = x.UserId,
                    Name = x.DisplayName,
                    TotalPoints = x.TotalPoints,
                    CurrentWeekPoints = x.CurrentWeekPoints,
                    WeeklyAverage = x.WeeksPlayed > 0
                        ? Math.Round((decimal)x.TotalPoints / x.WeeksPlayed, 1)
                        : 0,
                    Rank = rank,
                    TotalPicks = x.TotalPicks,
                    TotalCorrect = x.TotalCorrect,
                    PickAccuracy = x.TotalPicks > 0
                        ? Math.Round((decimal)x.TotalCorrect / x.TotalPicks * 100, 2)
                        : 0,
                    LastWeekRank = null
                });

                previousPoints = x.TotalPoints;
            }


            return result;
        }
    }
}
