using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.Picks.Queries.GetPickRecordWidget;

public interface IGetPickRecordWidgetQueryHandler
{
    Task<Result<PickRecordWidgetDto>> ExecuteAsync(
        GetPickRecordWidgetQuery query,
        CancellationToken cancellationToken = default);
}

public class GetPickRecordWidgetQueryHandler : IGetPickRecordWidgetQueryHandler
{
    private readonly ILogger<GetPickRecordWidgetQueryHandler> _logger;
    private readonly AppDataContext _dataContext;

    public GetPickRecordWidgetQueryHandler(
        ILogger<GetPickRecordWidgetQueryHandler> logger,
        AppDataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public async Task<Result<PickRecordWidgetDto>> ExecuteAsync(
        GetPickRecordWidgetQuery query,
        CancellationToken cancellationToken = default)
    {
        var userId = query.UserId;

        if (query.ForSynthetic)
        {
            var synthetic = await _dataContext.Users
                .AsNoTracking()
                .Where(u => u.IsSynthetic)
                .Select(u => u.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (synthetic == Guid.Empty)
            {
                _logger.LogError("Synthetic user not found");
                return new Failure<PickRecordWidgetDto>(
                    default!,
                    ResultStatus.NotFound,
                    [new ValidationFailure("synthetic", "Synthetic user not found.")]);
            }

            userId = synthetic;
        }

        var groupIds = await _dataContext.PickemGroupMembers
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .Select(x => x.PickemGroupId)
            .ToListAsync(cancellationToken);

        var items = new List<PickRecordWidgetDto.PickRecordWidgetItem>();
        int asOfWeek = 0;

        foreach (var groupId in groupIds)
        {
            var currentWeek = await _dataContext.UserPicks
                .AsNoTracking()
                .Where(p => p.PickemGroupId == groupId && p.PointsAwarded != null)
                .MaxAsync(p => (int?)p.Week, cancellationToken) ?? 0;

            if (asOfWeek == 0)
            {
                asOfWeek = currentWeek;
            }

            var groupName = await _dataContext.PickemGroups
                .AsNoTracking()
                .Where(g => g.Id == groupId)
                .Select(g => g.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? "Unknown";

            var userPicks = await _dataContext.UserPicks
                .AsNoTracking()
                .Where(p => p.PickemGroupId == groupId && p.UserId == userId)
                .ToListAsync(cancellationToken);

            var correct = userPicks.Count(x => x.IsCorrect == true);
            var incorrect = userPicks.Count(x => x.IsCorrect == false);
            var total = correct + incorrect;

            var widgetItem = new PickRecordWidgetDto.PickRecordWidgetItem
            {
                LeagueId = groupId,
                LeagueName = groupName,
                Correct = correct,
                Incorrect = incorrect,
                Accuracy = total > 0 ? Math.Round((double)correct / total, 2) : 0
            };

            items.Add(widgetItem);
        }

        var widget = new PickRecordWidgetDto
        {
            SeasonYear = query.SeasonYear,
            AsOfWeek = asOfWeek,
            Items = items.OrderBy(x => x.LeagueName).ToList()
        };

        return new Success<PickRecordWidgetDto>(widget);
    }
}
