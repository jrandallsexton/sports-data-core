using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Application.UI.Picks.PicksPage;
using SportsData.Api.Application.UI.Picks.Queries;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;

namespace SportsData.Api.Application.UI.Picks
{
    public interface IPickService
    {
        Task SubmitPickAsync(
            Guid userId,
            SubmitUserPickRequest request,
            CancellationToken cancellationToken);

        Task<List<UserPickDto>> GetUserPicksByGroupAndWeek(
            Guid userId,
            Guid groupId,
            int weekNumber,
            CancellationToken cancellationToken);

        Task<PickRecordWidgetDto> GetPickRecordWidget(
            Guid userId,
            CancellationToken cancellationToken);

        Task<PickRecordWidgetDto> GetPickRecordWidgetForSynthetic(
            Guid userId,
            CancellationToken cancellationToken);

        Task<List<PickAccuracyByWeekDto>> GetPickAccuracyByWeek(
            Guid userId,
            CancellationToken cancellationToken);

        Task<PickAccuracyByWeekDto> GetPickAccuracyByWeekForSynthetic(
            Guid userId,
            CancellationToken cancellationToken);
    }

    public class PickService : IPickService
    {
        private readonly ILogger<PickService> _logger;
        private readonly AppDataContext _dataContext;
        private readonly ISubmitUserPickCommandHandler _handler;
        private readonly IGetUserPicksQueryHandler _getUserPicksQueryHandler;

        public PickService(
            AppDataContext dataContext,
            ILogger<PickService> logger,
            ISubmitUserPickCommandHandler handler,
            IGetUserPicksQueryHandler getUserPicksQueryHandler)
        {
            _dataContext = dataContext;
            _logger = logger;
            _handler = handler;
            _getUserPicksQueryHandler = getUserPicksQueryHandler;
        }

        public async Task SubmitPickAsync(
            Guid userId,
            SubmitUserPickRequest request,
            CancellationToken cancellationToken)
        {

            // Validate that the PickemGroup exists and supports this PickType
            var group = await _dataContext.PickemGroups
                .FirstOrDefaultAsync(g => g.Id == request.PickemGroupId, cancellationToken)
                    ?? throw new InvalidOperationException("Pickem group not found.");

            //if ((group.PickTypesAllowed & request.PickType) == 0)
            //    throw new InvalidOperationException("This pick type is not allowed in the selected league.");

            // Optional: Validate the contest exists and isn't locked
            var matchup = await _dataContext.PickemGroupMatchups
                .FirstOrDefaultAsync(m => m.ContestId == request.ContestId, cancellationToken)
                    ?? throw new InvalidOperationException("Matchup not found for the specified contest");

            //if (matchup.IsLocked())
            //    throw new InvalidOperationException("This contest is locked and cannot be picked.");

            if (request.PickType == UserPickType.OverUnder && request.OverUnder == OverUnderPick.None)
            {
                throw new InvalidOperationException("PickType is OverUnder, but selection not provided");
            }

            // Dispatch command
            var command = new SubmitUserPickCommand
            {
                UserId = userId,
                PickemGroupId = request.PickemGroupId,
                ContestId = request.ContestId,
                Week = request.Week,
                PickType = request.PickType,

                FranchiseSeasonId = request.FranchiseSeasonId,
                OverUnder = request.OverUnder,
                ConfidencePoints = request.ConfidencePoints,

                TiebreakerGuessTotal = request.TiebreakerGuessTotal,
                TiebreakerGuessHome = request.TiebreakerGuessHome,
                TiebreakerGuessAway = request.TiebreakerGuessAway
            };

            await _handler.Handle(command, cancellationToken);
        }

        public async Task<List<UserPickDto>> GetUserPicksByGroupAndWeek(
            Guid userId,
            Guid groupId,
            int weekNumber,
            CancellationToken cancellationToken)
        {
            var query = new GetUserPicksQuery
            {
                UserId = userId,
                GroupId = groupId,
                WeekNumber = weekNumber
            };

            return await _getUserPicksQueryHandler.GetUserPicksByGroupAndWeek(query, cancellationToken);
        }

        public async Task<List<UserPickDto>> GetUserPicksByGroup(
            Guid groupId,
            Guid userId,
            CancellationToken cancellationToken)
        {
            return await _dataContext.UserPicks
                .AsNoTracking()
                .Include(x => x.User)
                .Where(p =>
                    p.PickemGroupId == groupId &&
                    p.UserId == userId)
                .Select(p => new UserPickDto
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    User = p.User.DisplayName,
                    ConfidencePoints = p.ConfidencePoints,
                    ContestId = p.ContestId,
                    FranchiseId = p.FranchiseId ?? Guid.Empty,
                    IsCorrect = p.IsCorrect,
                    PickType = p.PickType,
                    TiebreakerGuessTotal = p.TiebreakerGuessTotal,
                    PointsAwarded = p.PointsAwarded
                })
                .ToListAsync(cancellationToken);
        }

        public async Task<PickRecordWidgetDto> GetPickRecordWidget(
            Guid userId,
            CancellationToken cancellationToken)
        {

            var widget = new PickRecordWidgetDto
            {
                SeasonYear = 2025
            };

            var groupIds = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.PickemGroupId)
                .ToListAsync(cancellationToken);

            foreach (var groupId in groupIds)
            {
                // get the max week for the group where picks have been scored
                var currentWeek = await _dataContext.UserPicks
                    .AsNoTracking()
                    .Where(p => p.PickemGroupId == groupId && p.PointsAwarded != null)
                    .MaxAsync(p => (int?)p.Week, cancellationToken) ?? 0;

                if (widget.AsOfWeek == 0)
                {
                    widget.AsOfWeek = currentWeek;
                }

                var group = await _dataContext.PickemGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken: cancellationToken);

                var userPicks = (await GetUserPicksByGroup(groupId, userId, cancellationToken))
                                ?? [];

                var correct = userPicks.Count(x => x.IsCorrect == true);
                var incorrect = userPicks.Count(x => x.IsCorrect == false);
                var total = correct + incorrect;

                var widgetItem = new PickRecordWidgetDto.PickRecordWidgetItem
                {
                    LeagueId = groupId,
                    LeagueName = group!.Name,
                    Correct = correct,
                    Incorrect = incorrect,
                    Accuracy = total > 0 ? Math.Round((double)correct / total, 2) : 0
                };

                widget.Items.Add(widgetItem);
            }

            widget.Items = widget.Items.OrderBy(x => x.LeagueName).ToList();

            return widget;
        }

        public async Task<PickRecordWidgetDto> GetPickRecordWidgetForSynthetic(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var synthetic = await _dataContext.Users.Where(u => u.IsSynthetic).FirstOrDefaultAsync(cancellationToken);

            return await GetPickRecordWidget(synthetic!.Id, cancellationToken);
        }

        public async Task<List<PickAccuracyByWeekDto>> GetPickAccuracyByWeek(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var userName = await _dataContext.Users
                .Where(u => u.Id == userId)
                .Select(u => u.DisplayName)
                .FirstOrDefaultAsync(cancellationToken: cancellationToken) ?? "Unknown";

            var groupIds = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.PickemGroupId)
                .ToListAsync(cancellationToken: cancellationToken);

            var dtos = new List<PickAccuracyByWeekDto>();

            foreach (var groupId in groupIds)
            {
                var group = await _dataContext.PickemGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken: cancellationToken);

                var userPicks = await _dataContext.UserPicks
                    .AsNoTracking()
                    .Where(p => p.PickemGroupId == groupId && p.UserId == userId && p.PointsAwarded != null)
                    .ToListAsync(cancellationToken: cancellationToken);

                var groupedByWeek = userPicks
                    .GroupBy(p => p.Week)
                    .OrderBy(g => g.Key)
                    .Select(g =>
                    {
                        var weekPicks = g.ToList();
                        var correct = weekPicks.Count(p => p.IsCorrect == true);
                        var total = weekPicks.Count(p => p.IsCorrect != null); // only counted if judged

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
                    UserId = userId,
                    UserName = userName,
                    LeagueId = groupId,
                    LeagueName = group?.Name ?? "Unknown",
                    WeeklyAccuracy = groupedByWeek,
                    OverallAccuracyPercent = overall
                };

                dtos.Add(dto);
            }

            return dtos.OrderBy(dto => dto.LeagueName).ToList();
        }

        public async Task<PickAccuracyByWeekDto> GetPickAccuracyByWeekForSynthetic(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var synthetic = await _dataContext.Users
                .Where(u => u.IsSynthetic)
                .FirstOrDefaultAsync(cancellationToken);

            if (synthetic == null)
                throw new InvalidOperationException("Synthetic user not found.");

            var userLeagueIds = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.PickemGroupId)
                .ToListAsync(cancellationToken);

            var syntheticPicks = await _dataContext.UserPicks
                .AsNoTracking()
                .Where(p =>
                    userLeagueIds.Contains(p.PickemGroupId) &&
                    p.UserId == synthetic.Id &&
                    p.PointsAwarded != null)
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

            return new PickAccuracyByWeekDto
            {
                UserId = synthetic.Id,
                UserName = synthetic.DisplayName,
                LeagueId = Guid.Empty, // no single league applies
                LeagueName = "All Groups", // matches your chart
                WeeklyAccuracy = groupedByWeek,
                OverallAccuracyPercent = 0 // intentionally unused
            };
        }

    }
}
