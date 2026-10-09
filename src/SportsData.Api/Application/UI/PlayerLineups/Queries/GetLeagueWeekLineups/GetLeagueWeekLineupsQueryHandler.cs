using FluentValidation;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.PlayerLineups.Dtos;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetMyPlayerLineup;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.PlayerLineups.Queries.GetLeagueWeekLineups;

public record GetLeagueWeekLineupsQuery(Guid LeagueId, Guid UserId, int SeasonYear, int SeasonWeek);

public class GetLeagueWeekLineupsQueryValidator : AbstractValidator<GetLeagueWeekLineupsQuery>
{
    public GetLeagueWeekLineupsQueryValidator()
    {
        RuleFor(x => x.LeagueId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.SeasonYear).InclusiveBetween(2000, 2100);
        RuleFor(x => x.SeasonWeek).InclusiveBetween(1, 30);
    }
}

public class LeagueWeekLineupsDto
{
    public Guid LeagueId { get; set; }
    public int SeasonYear { get; set; }
    public int SeasonWeek { get; set; }
    public List<MemberWeekLineupDto> Members { get; set; } = [];
}

public class MemberWeekLineupDto
{
    public Guid UserId { get; set; }
    public required string DisplayName { get; set; }
    public bool IsSynthetic { get; set; }
    /// <summary>Persisted weekly total (the scoring consumers keep it fresh).</summary>
    public decimal TotalPoints { get; set; }
    /// <summary>
    /// Another member's slots whose game hasn't locked yet are left out of
    /// <see cref="Slots"/> (same rule as team picks: others' choices stay
    /// private until kickoff-5); this counts them so the client can say so.
    /// Always 0 for the caller's own lineup.
    /// </summary>
    public int HiddenSlotCount { get; set; }
    public List<PlayerLineupSlotDto> Slots { get; set; } = [];
}

public interface IGetLeagueWeekLineupsQueryHandler
{
    Task<Result<LeagueWeekLineupsDto>> ExecuteAsync(
        GetLeagueWeekLineupsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Every member's lineup for one league week: the Player Pick'em analogue of
/// the team-picks week overview (players and points instead of games and
/// picks). Members without a lineup still appear, empty.
/// </summary>
public class GetLeagueWeekLineupsQueryHandler : IGetLeagueWeekLineupsQueryHandler
{
    private readonly AppDataContext _dataContext;
    private readonly ILeagueMembershipGuard _membershipGuard;
    private readonly IValidator<GetLeagueWeekLineupsQuery> _validator;
    private readonly IDateTimeProvider _dateTimeProvider;

    public GetLeagueWeekLineupsQueryHandler(
        AppDataContext dataContext,
        ILeagueMembershipGuard membershipGuard,
        IValidator<GetLeagueWeekLineupsQuery> validator,
        IDateTimeProvider dateTimeProvider)
    {
        _dataContext = dataContext;
        _membershipGuard = membershipGuard;
        _validator = validator;
        _dateTimeProvider = dateTimeProvider;
    }

    public async Task<Result<LeagueWeekLineupsDto>> ExecuteAsync(
        GetLeagueWeekLineupsQuery query,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(query, cancellationToken);
        if (!validation.IsValid)
        {
            return new Failure<LeagueWeekLineupsDto>(default!, ResultStatus.Validation, validation.Errors);
        }

        var gate = await PlayerLineupGate.CheckAsync(
            _dataContext, _membershipGuard, query.LeagueId, query.UserId, cancellationToken);
        if (gate.Failure is not null)
        {
            return new Failure<LeagueWeekLineupsDto>(default!, gate.Failure.Value.Status, gate.Failure.Value.Errors);
        }

        var members = await (
                from m in _dataContext.PickemGroupMembers.AsNoTracking()
                join u in _dataContext.Users.AsNoTracking() on m.UserId equals u.Id
                where m.PickemGroupId == query.LeagueId
                select new { u.Id, u.DisplayName, u.IsSynthetic })
            .ToListAsync(cancellationToken);

        var lineups = await _dataContext.PlayerLineups
            .AsNoTracking()
            .Include(l => l.Slots)
            .Where(l => l.PickemGroupId == query.LeagueId &&
                        l.SeasonYear == query.SeasonYear &&
                        l.SeasonWeek == query.SeasonWeek)
            .ToListAsync(cancellationToken);
        var lineupByUser = lineups.ToDictionary(l => l.UserId);

        var now = _dateTimeProvider.UtcNow();
        var rows = members.Select(m =>
        {
            lineupByUser.TryGetValue(m.Id, out var lineup);
            var slots = (lineup?.Slots ?? [])
                .Select(s => s.ToDto(now))
                .ToList();
            var isCaller = m.Id == query.UserId;
            var visible = isCaller ? slots : slots.Where(s => s.IsLocked).ToList();

            return new MemberWeekLineupDto
            {
                UserId = m.Id,
                DisplayName = m.DisplayName,
                IsSynthetic = m.IsSynthetic,
                TotalPoints = lineup?.TotalPoints ?? 0m,
                HiddenSlotCount = slots.Count - visible.Count,
                Slots = visible,
            };
        })
        .OrderByDescending(r => r.TotalPoints)
        .ThenBy(r => r.DisplayName, StringComparer.OrdinalIgnoreCase)
        .ToList();

        return new Success<LeagueWeekLineupsDto>(new LeagueWeekLineupsDto
        {
            LeagueId = query.LeagueId,
            SeasonYear = query.SeasonYear,
            SeasonWeek = query.SeasonWeek,
            Members = rows,
        });
    }
}
