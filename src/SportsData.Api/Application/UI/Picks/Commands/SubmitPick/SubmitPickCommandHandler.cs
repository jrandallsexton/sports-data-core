using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Picks;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Picks.Commands.SubmitPick;

public interface ISubmitPickCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        SubmitPickCommand command,
        CancellationToken cancellationToken = default);
}

public class SubmitPickCommandHandler : ISubmitPickCommandHandler
{
    private readonly ILogger<SubmitPickCommandHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IEventBus _eventBus;

    public SubmitPickCommandHandler(
        ILogger<SubmitPickCommandHandler> logger,
        AppDataContext dataContext,
        IEventBus eventBus)
    {
        _logger = logger;
        _dataContext = dataContext;
        _eventBus = eventBus;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        SubmitPickCommand command,
        CancellationToken cancellationToken = default)
    {
        var group = await _dataContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == command.PickemGroupId, cancellationToken);

        if (group is null)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.PickemGroupId), "Pickem group not found.")]);
        }

        var matchup = await _dataContext.PickemGroupMatchups
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.ContestId == command.ContestId, cancellationToken);

        if (matchup is null)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.ContestId), "Matchup not found for the specified contest")]);
        }

        if (matchup.IsLocked())
        {
            return new Failure<Guid>(
                default,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.ContestId), "This contest is locked and cannot be picked.")]);
        }

        if (command.PickType == PickType.OverUnder && command.OverUnder == OverUnderPick.None)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.OverUnder), "PickType is OverUnder, but selection not provided")]);
        }

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

                TiebreakerType = TiebreakerType.TotalPoints
            };

            await _dataContext.UserPicks.AddAsync(pick, cancellationToken);
        }

        // Announce the active pick so Notification can project who picked this
        // contest (and target line-move / reminder notifications at pickers).
        // Published BEFORE the commit: with the EF outbox, Publish enqueues into
        // the DbContext tracker and SaveChangesAsync persists the pick and the
        // outbox row atomically. Publishing after SaveChangesAsync would lose
        // the event when the DI scope disposes.
        await _eventBus.Publish(
            new UserPickMade(
                UserId: command.UserId,
                ContestId: command.ContestId,
                PickemGroupId: command.PickemGroupId,
                Sport: group.Sport,
                SeasonYear: matchup.SeasonYear,
                CorrelationId: Guid.NewGuid(),
                CausationId: Guid.NewGuid()),
            cancellationToken);

        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<Guid>(command.ContestId);
    }
}
