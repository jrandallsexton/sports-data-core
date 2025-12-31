using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leaderboard.Dtos;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

using static SportsData.Api.Application.UI.Leaderboard.Dtos.LeaderboardWidgetDto;

namespace SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboardWidget;

public interface IGetLeaderboardWidgetQueryHandler
{
    Task<Result<LeaderboardWidgetDto>> ExecuteAsync(
        GetLeaderboardWidgetQuery query,
        CancellationToken cancellationToken = default);
}

public class GetLeaderboardWidgetQueryHandler : IGetLeaderboardWidgetQueryHandler
{
    private readonly ILogger<GetLeaderboardWidgetQueryHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IGetLeaderboardQueryHandler _getLeaderboardQueryHandler;

    public GetLeaderboardWidgetQueryHandler(
        ILogger<GetLeaderboardWidgetQueryHandler> logger,
        AppDataContext dataContext,
        IGetLeaderboardQueryHandler getLeaderboardQueryHandler)
    {
        _logger = logger;
        _dataContext = dataContext;
        _getLeaderboardQueryHandler = getLeaderboardQueryHandler;
    }

    public async Task<Result<LeaderboardWidgetDto>> ExecuteAsync(
        GetLeaderboardWidgetQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query.UserId == Guid.Empty)
        {
            return new Failure<LeaderboardWidgetDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.UserId), "User ID cannot be empty")]);
        }

        var seasonYear = query.SeasonYear ?? DateTime.UtcNow.Year;

        if (seasonYear < 1900 || seasonYear > 2100)
        {
            return new Failure<LeaderboardWidgetDto>(
                default!,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(query.SeasonYear), "Season year must be between 1900 and 2100")]);
        }

        // Verify user exists
        var userExists = await _dataContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.Id == query.UserId, cancellationToken);

        if (!userExists)
        {
            _logger.LogWarning("User not found: {UserId}", query.UserId);
            return new Failure<LeaderboardWidgetDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.UserId), "User not found")]);
        }

        var widget = new LeaderboardWidgetDto
        {
            AsOfWeek = 2,
            SeasonYear = seasonYear
        };

        var groupIds = await _dataContext.PickemGroupMembers
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId)
            .Select(x => x.PickemGroupId)
            .ToListAsync(cancellationToken);

        foreach (var groupId in groupIds)
        {
            var leaderboardQuery = new GetLeaderboardQuery { GroupId = groupId };
            var leaderboardResult = await _getLeaderboardQueryHandler.ExecuteAsync(leaderboardQuery, cancellationToken);

            if (!leaderboardResult.IsSuccess)
            {
                _logger.LogWarning("Could not retrieve leaderboard for group {GroupId}", groupId);
                continue;
            }

            var leaderboard = leaderboardResult.Value;

            var entry = leaderboard
                .FirstOrDefault(x => x.UserId == query.UserId);

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

        _logger.LogDebug("Retrieved leaderboard widget for user {UserId} with {Count} items", query.UserId, widget.Items.Count);

        return new Success<LeaderboardWidgetDto>(widget);
    }
}
