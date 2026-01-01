using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Picks.Queries.GetPickAccuracyByWeek;

public interface IGetPickAccuracyByWeekQueryHandler
{
    Task<Result<List<PickAccuracyByWeekDto>>> ExecuteAsync(
        GetPickAccuracyByWeekQuery query,
        CancellationToken cancellationToken = default);

    Task<Result<PickAccuracyByWeekDto>> ExecuteForSyntheticAsync(
        GetPickAccuracyByWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetPickAccuracyByWeekQueryHandler : IGetPickAccuracyByWeekQueryHandler
{
    private readonly ILogger<GetPickAccuracyByWeekQueryHandler> _logger;
    private readonly AppDataContext _dataContext;

    public GetPickAccuracyByWeekQueryHandler(
        ILogger<GetPickAccuracyByWeekQueryHandler> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<Result<List<PickAccuracyByWeekDto>>> ExecuteAsync(
        GetPickAccuracyByWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var userName = await _dataContext.Users
            .AsNoTracking()
            .Where(u => u.Id == query.UserId)
            .Select(u => u.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

        var groupIds = await _dataContext.PickemGroupMembers
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId)
            .Select(x => x.PickemGroupId)
            .ToListAsync(cancellationToken);

        var dtos = new List<PickAccuracyByWeekDto>();

        foreach (var groupId in groupIds)
        {
            var groupName = await _dataContext.PickemGroups
                .AsNoTracking()
                .Where(g => g.Id == groupId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

            var userPicks = await _dataContext.UserPicks
                .AsNoTracking()
                .Where(p => p.PickemGroupId == groupId && p.UserId == query.UserId && p.PointsAwarded != null)
                .ToListAsync(cancellationToken);

            var groupedByWeek = userPicks
                .GroupBy(p => p.Week)
                .OrderBy(g => g.Key)
                .Select(g =>
                {
                    var weekPicks = g.ToList();
                    var correct = weekPicks.Count(p => p.IsCorrect == true);
                    var total = weekPicks.Count(p => p.IsCorrect != null);

                    return new PickAccuracyByWeekDto.WeeklyAccuracyDto
                    {
                        Week = g.Key,
                        CorrectPicks = correct,
                        TotalPicks = total,
                        AccuracyPercent = total > 0
                            ? Math.Round((double)correct / total * 100, 1)
                            : 0
                    };
                })
                .ToList();

            var totalCorrect = userPicks.Count(p => p.IsCorrect == true);
            var totalPicks = userPicks.Count(p => p.IsCorrect != null);
            var overall = totalPicks > 0
                ? Math.Round((double)totalCorrect / totalPicks * 100, 1)
                : 0;

            var dto = new PickAccuracyByWeekDto
            {
                UserId = query.UserId,
                UserName = userName,
                LeagueId = groupId,
                LeagueName = groupName,
                WeeklyAccuracy = groupedByWeek,
                OverallAccuracyPercent = overall
            };

            dtos.Add(dto);
        }

        var result = dtos.OrderBy(dto => dto.LeagueName).ToList();
        return new Success<List<PickAccuracyByWeekDto>>(result);
    }

    public async Task<Result<PickAccuracyByWeekDto>> ExecuteForSyntheticAsync(
        GetPickAccuracyByWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        var synthetic = await _dataContext.Users
            .AsNoTracking()
            .Where(u => u.IsSynthetic)
            .FirstOrDefaultAsync(cancellationToken);

        if (synthetic == null)
        {
            _logger.LogError("Synthetic user not found");
            return new Failure<PickAccuracyByWeekDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure("synthetic", "Synthetic user not found.")]);
        }

        var syntheticPicks = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.PointsAwarded != null)
            .ToListAsync(cancellationToken);

        var groupedByWeek = syntheticPicks
            .GroupBy(p => p.Week)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var picks = g.ToList();
                var correct = picks.Count(p => p.IsCorrect == true);
                var total = picks.Count(p => p.IsCorrect != null);

                return new PickAccuracyByWeekDto.WeeklyAccuracyDto
                {
                    Week = g.Key,
                    CorrectPicks = correct,
                    TotalPicks = total,
                    AccuracyPercent = total > 0
                        ? Math.Round((double)correct / total * 100, 1)
                        : 0
                };
            })
            .ToList();

        // Calculate overall accuracy the same way as in ExecuteAsync
        var totalCorrect = syntheticPicks.Count(p => p.IsCorrect == true);
        var totalPicks = syntheticPicks.Count(p => p.IsCorrect != null);
        var overallPercent = totalPicks > 0
            ? Math.Round((double)totalCorrect / totalPicks * 100, 1)
            : 0;

        var result = new PickAccuracyByWeekDto
        {
            UserId = synthetic.Id,
            UserName = synthetic.DisplayName,
            LeagueId = Guid.Empty,
            LeagueName = "All Groups",
            WeeklyAccuracy = groupedByWeek,
            OverallAccuracyPercent = overallPercent
        };

        return new Success<PickAccuracyByWeekDto>(result);
    }
}
