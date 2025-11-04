using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leaderboard.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using static SportsData.Api.Application.UI.Leaderboard.Dtos.LeaderboardWidgetDto;

namespace SportsData.Api.Application.UI.Leaderboard
{
    public interface ILeaderboardService
    {
        Task<Result<List<LeaderboardUserDto>>> GetLeaderboardAsync(
            Guid groupId,
            CancellationToken cancellationToken);

        Task<Result<LeaderboardWidgetDto>> GetLeaderboardWidgetForUser(
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

        public async Task<Result<List<LeaderboardUserDto>>> GetLeaderboardAsync(
            Guid groupId,
            CancellationToken cancellationToken)
        {
            if (groupId == Guid.Empty)
            {
                return new Failure<List<LeaderboardUserDto>>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(groupId), "Group ID cannot be empty")]);
            }

            try
            {
                // Verify group exists
                var groupExists = await _dataContext.PickemGroups
                    .AsNoTracking()
                    .AnyAsync(g => g.Id == groupId, cancellationToken);

                if (!groupExists)
                {
                    _logger.LogWarning("Group not found: {GroupId}", groupId);
                    return new Failure<List<LeaderboardUserDto>>(
                        default!,
                        ResultStatus.NotFound,
                        [new ValidationFailure(nameof(groupId), "Group not found")]);
                }

                // get the max week for the group where picks have been scored
                var currentWeek = await _dataContext.UserPicks
                    .AsNoTracking()
                    .Where(p => p.PickemGroupId == groupId && p.PointsAwarded != null)
                    .MaxAsync(p => (int?)p.Week, cancellationToken) ?? 0;

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

                return new Success<List<LeaderboardUserDto>>(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard for group {GroupId}", groupId);
                return new Failure<List<LeaderboardUserDto>>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(groupId), $"Error retrieving leaderboard: {ex.Message}")]);
            }
        }

        public async Task<Result<LeaderboardWidgetDto>> GetLeaderboardWidgetForUser(
            Guid userId,
            int seasonYear,
            CancellationToken cancellationToken)
        {
            if (userId == Guid.Empty)
            {
                return new Failure<LeaderboardWidgetDto>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(userId), "User ID cannot be empty")]);
            }

            if (seasonYear < 1900 || seasonYear > 2100)
            {
                return new Failure<LeaderboardWidgetDto>(
                    default!,
                    ResultStatus.Validation,
                    [new ValidationFailure(nameof(seasonYear), "Season year must be between 1900 and 2100")]);
            }

            try
            {
                // Verify user exists
                var userExists = await _dataContext.Users
                    .AsNoTracking()
                    .AnyAsync(u => u.Id == userId, cancellationToken);

                if (!userExists)
                {
                    _logger.LogWarning("User not found: {UserId}", userId);
                    return new Failure<LeaderboardWidgetDto>(
                        default!,
                        ResultStatus.NotFound,
                        [new ValidationFailure(nameof(userId), "User not found")]);
                }

                var widget = new LeaderboardWidgetDto
                {
                    AsOfWeek = 2,
                    SeasonYear = seasonYear
                };

                var groupIds = await _dataContext.PickemGroupMembers
                    .AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .Select(x => x.PickemGroupId)
                    .ToListAsync(cancellationToken);

                foreach (var groupId in groupIds)
                {
                    var leaderboardResult = await GetLeaderboardAsync(groupId, cancellationToken);

                    if (!leaderboardResult.IsSuccess)
                    {
                        _logger.LogWarning("Could not retrieve leaderboard for group {GroupId}", groupId);
                        continue;
                    }

                    var leaderboard = leaderboardResult.Value;

                    var entry = leaderboard
                        .FirstOrDefault(x => x.UserId == userId);

                    if (entry == null)
                        continue;

                    widget.Items.Add(new LeaderboardWidgetItem()
                    {
                        LeagueId = entry.LeagueId,
                        Name = entry.LeagueName,
                        Rank = entry.Rank
                    });
                }

                widget.Items = widget.Items.OrderBy(x => x.Name).ToList();

                return new Success<LeaderboardWidgetDto>(widget);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving leaderboard widget for user {UserId}", userId);
                return new Failure<LeaderboardWidgetDto>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(userId), $"Error retrieving leaderboard widget: {ex.Message}")]);
            }
        }
    }
}
