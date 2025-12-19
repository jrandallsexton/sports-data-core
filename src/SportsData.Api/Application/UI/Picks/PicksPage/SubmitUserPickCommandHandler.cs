using Microsoft.EntityFrameworkCore;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;

namespace SportsData.Api.Application.UI.Picks.PicksPage
{
    public interface ISubmitUserPickCommandHandler
    {
        Task Handle(SubmitUserPickCommand command, CancellationToken cancellationToken);
    }

    public class SubmitUserPickCommandHandler : ISubmitUserPickCommandHandler
    {
        private readonly ILogger<SubmitUserPickCommandHandler> _logger;
        private readonly AppDataContext _dataContext;

        public SubmitUserPickCommandHandler(
            AppDataContext dataContext,
            ILogger<SubmitUserPickCommandHandler> logger)
        {
            _dataContext = dataContext;
            _logger = logger;
        }

        public async Task Handle(SubmitUserPickCommand command, CancellationToken cancellationToken)
        {
            var existing = await _dataContext.UserPicks
                .FirstOrDefaultAsync(p =>
                        p.UserId == command.UserId &&
                        p.PickemGroupId == command.PickemGroupId &&
                        p.ContestId == command.ContestId,
                    cancellationToken);

            if (existing is not null)
            {
                existing.FranchiseId = command.FranchiseSeasonId;
                existing.OverUnder = command.OverUnder;
                existing.ConfidencePoints = command.ConfidencePoints;
                existing.PickType = command.PickType;
                existing.TiebreakerGuessTotal = command.TiebreakerGuessTotal;
                existing.TiebreakerGuessHome = command.TiebreakerGuessHome;
                existing.TiebreakerGuessAway = command.TiebreakerGuessAway;

                // optionally update ScoredAt or PointsAwarded if re-scoring
            }
            else
            {
                var pick = new PickemGroupUserPick
                {
                    Id = Guid.NewGuid(),
                    UserId = command.UserId,
                    PickemGroupId = command.PickemGroupId,
                    ContestId = command.ContestId,
                    Week = command.Week,
                    PickType = command.PickType,

                    FranchiseId = command.FranchiseSeasonId,
                    OverUnder = command.OverUnder,
                    ConfidencePoints = command.ConfidencePoints,

                    TiebreakerGuessTotal = command.TiebreakerGuessTotal,
                    TiebreakerGuessHome = command.TiebreakerGuessHome,
                    TiebreakerGuessAway = command.TiebreakerGuessAway,

                    TiebreakerType = TiebreakerType.TotalPoints // could derive from group config in future
                };

                await _dataContext.UserPicks.AddAsync(pick, cancellationToken);
            }

            await _dataContext.SaveChangesAsync(cancellationToken);
        }
    }
}
