using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.PlayerLineups.Queries.GetMyPlayerLineup;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;

namespace SportsData.Api.Application.UI.PlayerLineups.Queries.GetPlayerStandings;

public record GetPlayerStandingsQuery(Guid LeagueId, Guid UserId, int SeasonYear);

public class PlayerStandingsDto
{
    public Guid LeagueId { get; set; }
    public int SeasonYear { get; set; }
    public List<PlayerStandingRowDto> Rows { get; set; } = [];
}

public class PlayerStandingRowDto
{
    public Guid UserId { get; set; }
    public required string DisplayName { get; set; }
    public decimal TotalPoints { get; set; }
    public int WeeklyWins { get; set; }
    public List<PlayerStandingWeekDto> Weeks { get; set; } = [];
}

public class PlayerStandingWeekDto
{
    public int Week { get; set; }
    public decimal Points { get; set; }
    /// <summary>Every slot frozen — the week's number can no longer move.</summary>
    public bool IsFinal { get; set; }
    /// <summary>Top score for the week (provisional while not final).</summary>
    public bool IsWeeklyWinner { get; set; }
}

public interface IGetPlayerStandingsQueryHandler
{
    Task<Result<PlayerStandingsDto>> ExecuteAsync(
        GetPlayerStandingsQuery query,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cumulative-points standings with weekly winners (the decided model —
/// see docs/features/player-pickem/scoring.md): season standing = sum
/// of persisted weekly lineup totals; each week's top score earns a
/// weekly-winner badge, provisional until the week's slots finalize.
/// Reads ONLY persisted totals — the scoring consumers keep them fresh,
/// so standings never re-aggregate statlines.
/// </summary>
public class GetPlayerStandingsQueryHandler : IGetPlayerStandingsQueryHandler
{
    private readonly AppDataContext _dataContext;
    private readonly ILeagueMembershipGuard _membershipGuard;

    public GetPlayerStandingsQueryHandler(
        AppDataContext dataContext,
        ILeagueMembershipGuard membershipGuard)
    {
        _dataContext = dataContext;
        _membershipGuard = membershipGuard;
    }

    public async Task<Result<PlayerStandingsDto>> ExecuteAsync(
        GetPlayerStandingsQuery query,
        CancellationToken cancellationToken = default)
    {
        var gate = await PlayerLineupGate.CheckAsync(
            _dataContext, _membershipGuard, query.LeagueId, query.UserId, cancellationToken);
        if (gate.Failure is not null)
        {
            return new Failure<PlayerStandingsDto>(default!, gate.Failure.Value.Status, gate.Failure.Value.Errors);
        }

        var lineups = await _dataContext.PlayerLineups
            .AsNoTracking()
            .Where(l => l.PickemGroupId == query.LeagueId && l.SeasonYear == query.SeasonYear)
            .Select(l => new
            {
                l.UserId,
                l.SeasonWeek,
                l.TotalPoints,
                HasSlots = l.Slots.Count > 0,
                AllFinal = l.Slots.Count > 0 && l.Slots.All(s => s.IsScoreFinal),
            })
            .ToListAsync(cancellationToken);

        var memberNames = await _dataContext.PickemGroupMembers
            .AsNoTracking()
            .Where(m => m.PickemGroupId == query.LeagueId)
            .Select(m => new { m.UserId, Name = m.User.DisplayName })
            .ToListAsync(cancellationToken);
        var nameByUser = memberNames.ToDictionary(m => m.UserId, m => m.Name);

        // Weekly winner = top TotalPoints among that week's non-empty
        // lineups (ties share the badge; provisional until every lineup
        // that week is final).
        var winnersByWeek = lineups
            .Where(l => l.HasSlots)
            .GroupBy(l => l.SeasonWeek)
            .ToDictionary(
                g => g.Key,
                g => g.Where(l => l.TotalPoints == g.Max(x => x.TotalPoints) && l.TotalPoints > 0)
                      .Select(l => l.UserId)
                      .ToHashSet());

        var rows = lineups
            .GroupBy(l => l.UserId)
            .Select(g => new PlayerStandingRowDto
            {
                UserId = g.Key,
                DisplayName = nameByUser.TryGetValue(g.Key, out var n) ? n : "Member",
                TotalPoints = g.Sum(l => l.TotalPoints),
                Weeks = g.OrderBy(l => l.SeasonWeek)
                    .Select(l => new PlayerStandingWeekDto
                    {
                        Week = l.SeasonWeek,
                        Points = l.TotalPoints,
                        IsFinal = l.AllFinal,
                        IsWeeklyWinner = winnersByWeek.TryGetValue(l.SeasonWeek, out var w) && w.Contains(g.Key),
                    })
                    .ToList(),
            })
            .Select(r =>
            {
                r.WeeklyWins = r.Weeks.Count(w => w.IsWeeklyWinner);
                return r;
            })
            .OrderByDescending(r => r.TotalPoints)
            .ToList();

        return new Success<PlayerStandingsDto>(new PlayerStandingsDto
        {
            LeagueId = query.LeagueId,
            SeasonYear = query.SeasonYear,
            Rows = rows,
        });
    }
}
