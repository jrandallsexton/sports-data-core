using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.PlayerLineups.Dtos;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetMyPlayerLineup;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.PlayerLineups.Commands.UpsertLineupSlot;

public interface IUpsertLineupSlotCommandHandler
{
    Task<Result<PlayerLineupSlotDto>> ExecuteAsync(
        UpsertLineupSlotCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Assign or replace one lineup slot. The write-side rules from the
/// design doc, in order: slot exists in the fixed shape; position
/// eligible; no duplicate athlete across slots; TARGET slot not locked;
/// INCOMING athlete's game not locked. Lock evaluation is fully
/// server-side: one GetMatchupsForSeasonWeek call resolves every team's
/// contest and kickoff, and the stored anchor is what this handler
/// resolved — never what the client sent. Matchup resolution failure
/// FAILS CLOSED (unlike the read-side clone): we will not accept a write
/// we cannot lock-check.
/// </summary>
public class UpsertLineupSlotCommandHandler : IUpsertLineupSlotCommandHandler
{
    private readonly ILogger<UpsertLineupSlotCommandHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly ILeagueMembershipGuard _membershipGuard;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IValidator<UpsertLineupSlotCommand> _validator;

    public UpsertLineupSlotCommandHandler(
        ILogger<UpsertLineupSlotCommandHandler> logger,
        AppDataContext dataContext,
        ILeagueMembershipGuard membershipGuard,
        IContestClientFactory contestClientFactory,
        IDateTimeProvider dateTimeProvider,
        IValidator<UpsertLineupSlotCommand> validator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _membershipGuard = membershipGuard;
        _contestClientFactory = contestClientFactory;
        _dateTimeProvider = dateTimeProvider;
        _validator = validator;
    }

    public async Task<Result<PlayerLineupSlotDto>> ExecuteAsync(
        UpsertLineupSlotCommand command,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<PlayerLineupSlotDto>(default!, ResultStatus.Validation, validation.Errors);
        }

        var gate = await PlayerLineupGate.CheckAsync(
            _dataContext, _membershipGuard, command.LeagueId, command.UserId, cancellationToken);
        if (gate.Failure is not null)
        {
            return new Failure<PlayerLineupSlotDto>(default!, gate.Failure.Value.Status, gate.Failure.Value.Errors);
        }

        // Canonical slot id from here on — persisting the caller's casing
        // would let "qb" and "QB" become two distinct QB slots under the
        // case-sensitive unique index.
        var slotId = LineupSlots.Normalize(command.SlotId);
        if (slotId is null)
        {
            return Fail(ResultStatus.Validation, nameof(command.SlotId),
                $"Unknown slot '{command.SlotId}'.");
        }

        if (!LineupSlots.IsEligible(slotId, command.Position))
        {
            return Fail(ResultStatus.Validation, nameof(command.Position),
                $"Position '{command.Position}' is not eligible for slot '{slotId}'.");
        }

        // ── Server-side matchup resolution (the lock authority) ───────────
        WeekMatchupMap weekMap;
        try
        {
            var matchups = await _contestClientFactory
                .Resolve(gate.Group!.Sport)
                .GetMatchupsForSeasonWeek(command.SeasonYear, command.SeasonWeek, cancellationToken);

            if (!matchups.IsSuccess)
            {
                _logger.LogError(
                    "Slot upsert rejected: matchup resolution failed. LeagueId={LeagueId} Week={Week} Status={Status}",
                    command.LeagueId, command.SeasonWeek, matchups.Status);
                return Fail(ResultStatus.Error, nameof(command.SlotId),
                    "Unable to verify game locks right now. Please try again.");
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
                "Slot upsert rejected: matchup resolution threw. LeagueId={LeagueId} Week={Week}",
                command.LeagueId, command.SeasonWeek);
            return Fail(ResultStatus.Error, nameof(command.SlotId),
                "Unable to verify game locks right now. Please try again.");
        }

        var now = _dateTimeProvider.UtcNow();

        var lineup = await _dataContext.PlayerLineups
            .Include(l => l.Slots)
            .FirstOrDefaultAsync(l =>
                    l.PickemGroupId == command.LeagueId &&
                    l.UserId == command.UserId &&
                    l.SeasonYear == command.SeasonYear &&
                    l.SeasonWeek == command.SeasonWeek,
                cancellationToken);

        // ── Duplicate athlete across slots ────────────────────────────────
        if (lineup is not null && lineup.Slots.Any(s =>
                s.SlotId != slotId && s.AthleteId == command.AthleteId))
        {
            return Fail(ResultStatus.Validation, nameof(command.AthleteId),
                "That athlete is already rostered in another slot.");
        }

        // ── Target slot lock (both the stored anchor AND the current
        //    resolution of the OCCUPANT's team — a cloned slot can carry a
        //    null anchor, and null must never mean unlocked-forever) ───────
        var existing = lineup?.Slots.FirstOrDefault(s => s.SlotId == slotId);
        if (existing is not null && IsSlotLocked(existing, weekMap, now))
        {
            return Fail(ResultStatus.Validation, nameof(command.SlotId),
                $"Slot '{slotId}' is locked — {existing.LastName}'s game has started or starts within 5 minutes.");
        }

        // ── Incoming athlete's game lock ──────────────────────────────────
        var incoming = weekMap.Resolve(command.TeamSlug);
        if (incoming is not null &&
            PickemGroupMatchupExtensions.IsStartLocked(incoming.Value.StartUtc, now))
        {
            return Fail(ResultStatus.Validation, nameof(command.AthleteId),
                "That athlete's game has started or starts within 5 minutes.");
        }

        // ── Upsert ────────────────────────────────────────────────────────
        if (lineup is null)
        {
            lineup = new PlayerLineup
            {
                Id = Guid.NewGuid(),
                PickemGroupId = command.LeagueId,
                UserId = command.UserId,
                SeasonYear = command.SeasonYear,
                SeasonWeek = command.SeasonWeek,
                CreatedUtc = now,
                CreatedBy = command.UserId,
            };
            await _dataContext.PlayerLineups.AddAsync(lineup, cancellationToken);
        }

        if (existing is null)
        {
            existing = new PlayerLineupSlot
            {
                Id = Guid.NewGuid(),
                PlayerLineupId = lineup.Id,
                SlotId = slotId,
                CreatedUtc = now,
                CreatedBy = command.UserId,
            };
            // Explicit DbSet.Add, NOT just lineup.Slots.Add: when the
            // lineup is ALREADY TRACKED, an entity discovered through
            // navigation fixup with a client-set Guid key is marked
            // Modified (EF can't tell new from pre-existing), producing
            // UPDATE ... WHERE Id = <fresh guid> → 0 rows →
            // DbUpdateConcurrencyException. DbSet.Add pins Added. (The
            // new-lineup path never hit this — everything reachable from
            // an explicitly Add()ed root is Added.)
            _dataContext.PlayerLineupSlots.Add(existing);
            lineup.Slots.Add(existing);
        }

        existing.AthleteId = command.AthleteId;
        existing.AthleteSeasonId = command.AthleteSeasonId;
        existing.Position = command.Position;
        existing.FirstName = command.FirstName;
        existing.LastName = command.LastName;
        existing.TeamName = command.TeamName;
        existing.TeamSlug = command.TeamSlug;
        existing.ContestId = incoming?.ContestId;         // server-resolved
        existing.ContestStartUtc = incoming?.StartUtc;    // server-resolved
        existing.OpponentName = command.OpponentName;     // display-only
        existing.ModifiedUtc = now;
        existing.ModifiedBy = command.UserId;
        lineup.ModifiedUtc = now;
        lineup.ModifiedBy = command.UserId;

        await _dataContext.SaveChangesAsync(cancellationToken);

        return new Success<PlayerLineupSlotDto>(existing.ToDto(now));
    }

    /// <summary>
    /// A slot is locked when EITHER its stored anchor says so or the
    /// occupant's team resolves to a locked game this week. The second
    /// check closes the carry-over hole: cloned slots can hold a null
    /// anchor, and without it a user could swap out a player mid-game.
    /// </summary>
    internal static bool IsSlotLocked(PlayerLineupSlot slot, WeekMatchupMap weekMap, DateTime nowUtc)
    {
        if (slot.ContestStartUtc.HasValue &&
            PickemGroupMatchupExtensions.IsStartLocked(slot.ContestStartUtc.Value, nowUtc))
        {
            return true;
        }

        var current = weekMap.Resolve(slot.TeamSlug);
        return current is not null &&
               PickemGroupMatchupExtensions.IsStartLocked(current.Value.StartUtc, nowUtc);
    }

    private static Failure<PlayerLineupSlotDto> Fail(ResultStatus status, string property, string message) =>
        new(default!, status, [new ValidationFailure(property, message)]);
}
