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
        Task SubmitPickAsync(Guid userId, SubmitUserPickRequest request, CancellationToken cancellationToken);

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

            if (matchup.IsLocked())
                throw new InvalidOperationException("This contest is locked and cannot be picked.");

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

        public async Task<PickRecordWidgetDto> GetPickRecordWidget(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var widget = new PickRecordWidgetDto
            {
                AsOfWeek = 1,
                SeasonYear = 2025
            };

            var groupIds = await _dataContext.PickemGroupMembers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.PickemGroupId)
                .ToListAsync(cancellationToken);

            foreach (var groupId in groupIds)
            {
                var group = await _dataContext.PickemGroups
                    .AsNoTracking()
                    .FirstOrDefaultAsync(g => g.Id == groupId, cancellationToken: cancellationToken);

                var userPicks = (await GetUserPicksByGroupAndWeek(userId, groupId, 1, cancellationToken))
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

        public async Task<PickRecordWidgetDto> GetPickRecordWidgetForSynthetic(Guid userId, CancellationToken cancellationToken)
        {
            var synthetic = await _dataContext.Users.Where(u => u.IsSynthetic).FirstOrDefaultAsync(cancellationToken);

            return await GetPickRecordWidget(synthetic!.Id, cancellationToken);
        }
    }
}
