using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leaderboard.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;

public interface IGetLeaderboardQueryHandler
{
    Task<Result<List<LeaderboardUserDto>>> ExecuteAsync(
        GetLeaderboardQuery query,
        CancellationToken cancellationToken = default);
}

public class GetLeaderboardQueryHandler : IGetLeaderboardQueryHandler
{
    private readonly ILogger<GetLeaderboardQueryHandler> _logger;
    private readonly AppDataContext _dataContext;

    public GetLeaderboardQueryHandler(
        ILogger<GetLeaderboardQueryHandler> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<Result<List<LeaderboardUserDto>>> ExecuteAsync(
        GetLeaderboardQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.GroupId == Guid.Empty)
        {
            return new Failure<List<LeaderboardUserDto>>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.GroupId), "Group ID cannot be empty")]);
        }

        // Verify group exists
        var groupExists = await _dataContext.PickemGroups
            .AsNoTracking()
            .AnyAsync(g => g.Id == query.GroupId, cancellationToken);

        if (!groupExists)
        {
            _logger.LogWarning("Group not found: {GroupId}", query.GroupId);
            return new Failure<List<LeaderboardUserDto>>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.GroupId), "Group not found")]);
        }

        // get the max week for the group where picks have been scored
        var currentWeek = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.PickemGroupId == query.GroupId && p.PointsAwarded != null)
            .MaxAsync(p => (int?)p.Week, cancellationToken) ?? 0;

        var leaderboard = await _dataContext.UserPicks
            .Include(p => p.Group)
            .Where(p => p.PickemGroupId == query.GroupId && p.PointsAwarded != null && p.ScoredAt != null)
            .GroupBy(p => new { p.UserId, p.User.DisplayName, p.User.IsSynthetic })
            .Select(g => new
            {
                LeagueId = query.GroupId,
                LeagueName = g.First().Group.Name,
                g.Key.UserId,
                g.Key.DisplayName,
                g.Key.IsSynthetic,
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
                IsSynthetic = x.IsSynthetic,
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

        _logger.LogDebug("Retrieved leaderboard for group {GroupId} with {Count} entries", query.GroupId, result.Count);

        return new Success<List<LeaderboardUserDto>>(result);
    }
}
