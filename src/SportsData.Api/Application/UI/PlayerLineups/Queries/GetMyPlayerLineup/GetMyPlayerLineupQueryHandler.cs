using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.PlayerLineups.Dtos;
using SportsData.Api.Application.UI.PlayerLineups.Scoring;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Athlete;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.PlayerLineups.Queries.GetMyPlayerLineup;

public interface IGetMyPlayerLineupQueryHandler
{
    Task<Result<PlayerLineupDto>> ExecuteAsync(
        GetMyPlayerLineupQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// The user's roster for a league-week, with per-slot locking DERIVED via
/// the product-wide kickoff−5 rule. This read is also where the LAZY
/// CARRY-OVER happens: no lineup for the requested week + a populated
/// earlier week exists → clone the most recent one, re-resolving every
/// athlete's contest for the NEW week (bye → null, badged). Lazy beats a
/// fleet-wide rollover job: clones only for users who show up, no Sunday
/// thundering herd, same experience.
/// See docs/features/player-pickem/roster-persistence.md.
/// </summary>
public class GetMyPlayerLineupQueryHandler : IGetMyPlayerLineupQueryHandler
{
    private readonly ILogger<GetMyPlayerLineupQueryHandler> _logger;
    private readonly AppDataContext _dataContext;
    private readonly ILeagueMembershipGuard _membershipGuard;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IAthleteClientFactory _athleteClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetMyPlayerLineupQueryHandler(
        ILogger<GetMyPlayerLineupQueryHandler> logger,
        AppDataContext dataContext,
        ILeagueMembershipGuard membershipGuard,
        IContestClientFactory contestClientFactory,
        IAthleteClientFactory athleteClientFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _membershipGuard = membershipGuard;
        _contestClientFactory = contestClientFactory;
        _athleteClientFactory = athleteClientFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<PlayerLineupDto>> ExecuteAsync(
        GetMyPlayerLineupQuery query,
        CancellationToken cancellationToken = default)
    {
        var gate = await PlayerLineupGate.CheckAsync(
            _dataContext, _membershipGuard, query.LeagueId, query.UserId, cancellationToken);
        if (gate.Failure is not null)
        {
            return new Failure<PlayerLineupDto>(default!, gate.Failure.Value.Status, gate.Failure.Value.Errors);
        }

        // Pure read — the result is only projected to the DTO (the clone
        // path constructs and Adds fresh entities), so nothing needs
        // tracking.
        var lineup = await _dataContext.PlayerLineups
            .AsNoTracking()
            .Include(l => l.Slots)
            .FirstOrDefaultAsync(l =>
                    l.PickemGroupId == query.LeagueId &&
                    l.UserId == query.UserId &&
                    l.SeasonYear == query.SeasonYear &&
                    l.SeasonWeek == query.SeasonWeek,
                cancellationToken);

        if (lineup is null)
        {
            lineup = await TryCloneFromPriorWeekAsync(query, gate.Group!, cancellationToken);
        }

        var now = _dateTimeProvider.UtcNow();
        var dto = new PlayerLineupDto
        {
            LeagueId = query.LeagueId,
            SeasonYear = query.SeasonYear,
            SeasonWeek = query.SeasonWeek,
            Slots = lineup?.Slots
                .OrderBy(s => s.SlotId)
                .Select(s => s.ToDto(now))
                .ToList() ?? [],
        };

        await ApplyLiveScoringAsync(dto, gate.Group!, cancellationToken);

        return new Success<PlayerLineupDto>(dto);
    }

    /// <summary>
    /// Read-time live scoring: one batch statline call for the lineup's
    /// anchored slots, priced by the league's scoring matrix (v1: the
    /// IsDefault set; per-league selection is a later FK). Fail OPEN —
    /// scoring is display enrichment, never a reason to 500 the roster.
    /// Persistence on game finalization is deliberately deferred
    /// (docs/features/player-pickem/scoring.md).
    /// </summary>
    private async Task ApplyLiveScoringAsync(
        PlayerLineupDto dto,
        PickemGroup group,
        CancellationToken cancellationToken)
    {
        // Persisted scores (Phase 2 consumers) win; live-compute only the
        // slots the consumers haven't touched yet (event lag / pre-Phase-2
        // rows). The read path never writes, so the two can't fight.
        // Persisted points count toward the total even if the live
        // computation below fails — set the floor first, refine after.
        dto.TotalPoints = dto.Slots.Sum(s => s.Points ?? 0m);

        var anchored = dto.Slots
            .Where(s => s.ContestId.HasValue && s.Points is null)
            .ToList();
        if (anchored.Count == 0) return;

        try
        {
            var rules = await _dataContext.PlayerScoringRules
                .AsNoTracking()
                .Where(r => r.RuleSet.IsDefault)
                .Select(r => new ScoringRule(r.StatKey, r.Points, r.PerUnits))
                .ToListAsync(cancellationToken);
            if (rules.Count == 0) return;

            var statlines = await _athleteClientFactory
                .Resolve(group.Sport)
                .GetAthleteStatlines(
                    anchored.Select(s => s.ContestId!.Value).Distinct().ToList(),
                    anchored.Select(s => s.AthleteSeasonId).Distinct().ToList(),
                    cancellationToken);
            if (!statlines.IsSuccess) return;

            var byKey = statlines.Value.ToDictionary(x => (x.AthleteSeasonId, x.ContestId));
            foreach (var slot in anchored)
            {
                if (!byKey.TryGetValue((slot.AthleteSeasonId, slot.ContestId!.Value), out var line))
                    continue;
                var score = PlayerPickemScoringEngine.Score(rules, line.Stats);
                slot.Points = score.Points;
                slot.StatLine = PlayerPickemScoringEngine.BuildStatLine(score.Contributions);
            }

            dto.TotalPoints = dto.Slots.Sum(s => s.Points ?? 0m);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Live scoring skipped for lineup. LeagueId={LeagueId} Week={Week}",
                dto.LeagueId, dto.SeasonWeek);
        }
    }

    private async Task<PlayerLineup?> TryCloneFromPriorWeekAsync(
        GetMyPlayerLineupQuery query,
        PickemGroup group,
        CancellationToken cancellationToken)
    {
        var prior = await _dataContext.PlayerLineups
            .AsNoTracking()
            .Include(l => l.Slots)
            .Where(l =>
                l.PickemGroupId == query.LeagueId &&
                l.UserId == query.UserId &&
                l.SeasonYear == query.SeasonYear &&
                l.SeasonWeek < query.SeasonWeek)
            .OrderByDescending(l => l.SeasonWeek)
            .FirstOrDefaultAsync(cancellationToken);

        if (prior is null || prior.Slots.Count == 0)
        {
            return null;
        }

        // Contest anchors are re-resolved for the NEW week from the server's
        // own matchup data — never carried from the prior week (a stale
        // anchor would lock the slot to last week's kickoff).
        WeekMatchupMap weekMap;
        try
        {
            var matchups = await LeagueWeekMatchupResolver.ResolveAsync(
                _dataContext,
                _contestClientFactory.Resolve(group.Sport),
                query.LeagueId, query.SeasonYear, query.SeasonWeek, cancellationToken);

            if (!matchups.IsSuccess)
            {
                // Fail open on the CLONE only: serve the empty week now and
                // let the next read retry — cloning is a convenience, not
                // correctness. (Writes fail closed; see the upsert handler.)
                _logger.LogWarning(
                    "Carry-over clone skipped: matchup resolution failed. LeagueId={LeagueId} Week={Week} Status={Status}",
                    query.LeagueId, query.SeasonWeek, matchups.Status);
                return null;
            }

            weekMap = new WeekMatchupMap(matchups.Value);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller went away — surface the cancellation instead of
            // serving a fabricated empty week.
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Carry-over clone skipped: matchup resolution threw. LeagueId={LeagueId} Week={Week}",
                query.LeagueId, query.SeasonWeek);
            return null;
        }

        var now = _dateTimeProvider.UtcNow();
        var lineup = new PlayerLineup
        {
            Id = Guid.NewGuid(),
            PickemGroupId = query.LeagueId,
            UserId = query.UserId,
            SeasonYear = query.SeasonYear,
            SeasonWeek = query.SeasonWeek,
            CreatedUtc = now,
            CreatedBy = query.UserId,
        };

        foreach (var s in prior.Slots)
        {
            var matchup = weekMap.Resolve(s.TeamSlug);
            lineup.Slots.Add(new PlayerLineupSlot
            {
                Id = Guid.NewGuid(),
                PlayerLineupId = lineup.Id,
                SlotId = s.SlotId,
                AthleteId = s.AthleteId,
                AthleteSeasonId = s.AthleteSeasonId,
                Position = s.Position,
                FirstName = s.FirstName,
                LastName = s.LastName,
                TeamName = s.TeamName,
                TeamSlug = s.TeamSlug,
                ContestId = matchup?.ContestId,
                ContestStartUtc = matchup?.StartUtc,
                // Opponent display is unknown until the UI enriches or the
                // user re-saves; the anchor fields above are what matter.
                OpponentName = null,
                CreatedUtc = now,
                CreatedBy = query.UserId,
            });
        }

        await _dataContext.PlayerLineups.AddAsync(lineup, cancellationToken);
        try
        {
            await _dataContext.SaveChangesAsync(cancellationToken);
            _logger.LogInformation(
                "Carried lineup over. LeagueId={LeagueId} UserId={UserId} FromWeek={FromWeek} ToWeek={ToWeek} Slots={Slots}",
                query.LeagueId, query.UserId, prior.SeasonWeek, query.SeasonWeek, lineup.Slots.Count);
        }
        catch (DbUpdateException)
        {
            // Two concurrent first-reads raced the unique index; the winner's
            // clone is canonical. Detach ours and reload.
            _dataContext.Entry(lineup).State = EntityState.Detached;
            foreach (var slot in lineup.Slots)
            {
                _dataContext.Entry(slot).State = EntityState.Detached;
            }

            lineup = await _dataContext.PlayerLineups
                .AsNoTracking()
                .Include(l => l.Slots)
                .FirstOrDefaultAsync(l =>
                        l.PickemGroupId == query.LeagueId &&
                        l.UserId == query.UserId &&
                        l.SeasonYear == query.SeasonYear &&
                        l.SeasonWeek == query.SeasonWeek,
                    cancellationToken);
        }

        return lineup;
    }
}

/// <summary>
/// Shared gate for every player-lineup operation: the league exists, IS a
/// PlayerPickem group (one game per league — see GroupType), and the
/// caller is a member. A TeamPickem league is Forbid, not NotFound, so
/// the client can distinguish "no such league" from "this league plays a
/// different game".
/// </summary>
internal static class PlayerLineupGate
{
    internal readonly record struct GateResult(
        PickemGroup? Group,
        (ResultStatus Status, List<ValidationFailure> Errors)? Failure);

    internal static async Task<GateResult> CheckAsync(
        AppDataContext dataContext,
        ILeagueMembershipGuard membershipGuard,
        Guid leagueId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        var group = await dataContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == leagueId, cancellationToken);

        if (group is null)
        {
            return new GateResult(null, (ResultStatus.NotFound,
                [new ValidationFailure(nameof(leagueId), "League not found.")]));
        }

        if (group.GroupType != Application.Common.Enums.GroupType.PlayerPickem)
        {
            return new GateResult(null, (ResultStatus.Forbid,
                [new ValidationFailure(nameof(leagueId), "This league does not play Player Pick'em.")]));
        }

        if (!await membershipGuard.IsMemberAsync(leagueId, userId, cancellationToken))
        {
            return new GateResult(null, (ResultStatus.Forbid,
                [new ValidationFailure(nameof(userId), "You are not a member of this league.")]));
        }

        return new GateResult(group, null);
    }
}

internal static class PlayerLineupSlotExtensions
{
    /// <summary>Locking derives from the same kickoff−5 rule team picks use.</summary>
    internal static PlayerLineupSlotDto ToDto(this PlayerLineupSlot s, DateTime nowUtc) => new()
    {
        SlotId = s.SlotId,
        AthleteId = s.AthleteId,
        AthleteSeasonId = s.AthleteSeasonId,
        Position = s.Position,
        FirstName = s.FirstName,
        LastName = s.LastName,
        TeamName = s.TeamName,
        TeamSlug = s.TeamSlug,
        ContestId = s.ContestId,
        ContestStartUtc = s.ContestStartUtc,
        OpponentName = s.OpponentName,
        Points = s.Points,
        StatLine = s.StatLine,
        IsLocked = s.ContestStartUtc.HasValue &&
                   PickemGroupMatchupExtensions.IsStartLocked(s.ContestStartUtc.Value, nowUtc),
    };
}
