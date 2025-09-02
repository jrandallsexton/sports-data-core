using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leaderboard.Dtos;
using SportsData.Api.Infrastructure.Data;

using static SportsData.Api.Application.UI.Leaderboard.Dtos.LeaderboardWidgetDto;

namespace SportsData.Api.Application.UI.Leaderboard
{
    public interface ILeaderboardService
    {
        Task<List<LeaderboardUserDto>> GetLeaderboardAsync(
            Guid groupId,
            int currentWeek,
            CancellationToken cancellationToken);

        Task<LeaderboardWidgetDto> GetLeaderboardWidgetForUser(
            Guid userId,
            int seasonYear,
            CancellationToken cancellationToken);
    }

    public class LeaderboardService : ILeaderboardService
    {
        private readonly ILogger<LeaderboardService> _logger;
        private readonly AppDataContext _dataContext;

        public LeaderboardService(
            ILogger<LeaderboardService> logger,
            AppDataContext dataContext)
        {
            _logger = logger;
            _dataContext = dataContext;
        }

        public async Task<List<LeaderboardUserDto>> GetLeaderboardAsync(
            Guid groupId,
            int currentWeek,
            CancellationToken cancellationToken)
        {
            var leaderboard = await _dataContext.UserPicks
                .Include(p => p.Group)
                .Where(p => p.PickemGroupId == groupId && p.PointsAwarded != null)
                .GroupBy(p => new { p.UserId, p.User.DisplayName })
                .Select(g => new
                {
                    LeagueId = groupId,
                    LeagueName = g.First().Group.Name,
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
                    LeagueId = x.LeagueId,
                    LeagueName = x.LeagueName,
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

        public async Task<LeaderboardWidgetDto> GetLeaderboardWidgetForUser(
            Guid userId,
            int seasonYear,
            CancellationToken cancellationToken)
        {
            var widget = new LeaderboardWidgetDto
            {
                AsOfWeek = 1,
                SeasonYear = seasonYear
            };

            var groupIds = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.PickemGroupId)
                .ToListAsync(cancellationToken);

            foreach (var groupId in groupIds)
            {
                var leaderboard = await GetLeaderboardAsync(groupId, 1, cancellationToken);

                var entry = leaderboard
                    .FirstOrDefault(x => x.UserId == userId);

                if (entry == null)
                    continue;

                widget.Items.Add(new WidgetItem()
                {
                    LeagueId = entry.LeagueId,
                    Name = entry.LeagueName,
                    Rank = entry.Rank
                });
            }

            widget.Items = widget.Items.OrderBy(x => x.Name).ToList();

            return widget;
        }
    }
}
