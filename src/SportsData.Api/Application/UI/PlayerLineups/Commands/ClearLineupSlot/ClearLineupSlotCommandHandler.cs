using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.PlayerLineups.Commands.UpsertLineupSlot;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetMyPlayerLineup;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.PlayerLineups.Commands.ClearLineupSlot;

public interface IClearLineupSlotCommandHandler
{
    Task<Result<bool>> ExecuteAsync(
        ClearLineupSlotCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Clear an unlocked slot. Same lock evaluation as the upsert (stored
/// anchor OR current resolution of the occupant's team), and the same
/// fail-closed posture when matchup resolution is unavailable.
/// </summary>
public class ClearLineupSlotCommandHandler : IClearLineupSlotCommandHandler
{
    private readonly ILogger<ClearLineupSlotCommandHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly ILeagueMembershipGuard _membershipGuard;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public ClearLineupSlotCommandHandler(
        ILogger<ClearLineupSlotCommandHandler> logger,
        AppDataContext dataContext,
        ILeagueMembershipGuard membershipGuard,
        IContestClientFactory contestClientFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _membershipGuard = membershipGuard;
        _contestClientFactory = contestClientFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<bool>> ExecuteAsync(
        ClearLineupSlotCommand command,
        CancellationToken cancellationToken = default)
    {
        var gate = await PlayerLineupGate.CheckAsync(
            _dataContext, _membershipGuard, command.LeagueId, command.UserId, cancellationToken);
        if (gate.Failure is not null)
        {
            return new Failure<bool>(default, gate.Failure.Value.Status, gate.Failure.Value.Errors);
        }

        var lineup = await _dataContext.PlayerLineups
            .Include(l => l.Slots)
            .FirstOrDefaultAsync(l =>
                    l.PickemGroupId == command.LeagueId &&
                    l.UserId == command.UserId &&
                    l.SeasonYear == command.SeasonYear &&
                    l.SeasonWeek == command.SeasonWeek,
                cancellationToken);

        var slot = lineup?.Slots.FirstOrDefault(s => s.SlotId == command.SlotId);
        if (lineup is null || slot is null)
        {
            return new Failure<bool>(default, ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.SlotId), "No athlete in that slot.")]);
        }

        WeekMatchupMap weekMap;
        try
        {
            var matchups = await _contestClientFactory
                .Resolve(gate.Group!.Sport)
                .GetMatchupsForSeasonWeek(command.SeasonYear, command.SeasonWeek, cancellationToken);

            if (!matchups.IsSuccess)
            {
                _logger.LogError(
                    "Slot clear rejected: matchup resolution failed. LeagueId={LeagueId} Week={Week} Status={Status}",
                    command.LeagueId, command.SeasonWeek, matchups.Status);
                return new Failure<bool>(default, ResultStatus.Error,
                    [new ValidationFailure(nameof(command.SlotId), "Unable to verify game locks right now. Please try again.")]);
            }

            weekMap = new WeekMatchupMap(matchups.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Slot clear rejected: matchup resolution threw. LeagueId={LeagueId} Week={Week}",
                command.LeagueId, command.SeasonWeek);
            return new Failure<bool>(default, ResultStatus.Error,
                [new ValidationFailure(nameof(command.SlotId), "Unable to verify game locks right now. Please try again.")]);
        }

        var now = _dateTimeProvider.UtcNow();
        if (UpsertLineupSlotCommandHandler.IsSlotLocked(slot, weekMap, now))
        {
            return new Failure<bool>(default, ResultStatus.Validation,
                [new ValidationFailure(nameof(command.SlotId),
                    $"Slot '{command.SlotId}' is locked — {slot.LastName}'s game has started or starts within 5 minutes.")]);
        }

        _dataContext.PlayerLineupSlots.Remove(slot);
        lineup.ModifiedUtc = now;
        lineup.ModifiedBy = command.UserId;
        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<bool>(true);
    }
}
