using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;

using SportsData.Api.Application.Common.Enums;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueScoresByWeek;

public interface IGetLeagueScoresByWeekQueryHandler
{
    Task<Result<LeagueScoresByWeekDto>> ExecuteAsync(
        GetLeagueScoresByWeekQuery query,
        CancellationToken cancellationToken = default);
}

public class GetLeagueScoresByWeekQueryHandler : IGetLeagueScoresByWeekQueryHandler
{
    private readonly ILogger<GetLeagueScoresByWeekQueryHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly ILeagueMembershipGuard _membershipGuard;

    public GetLeagueScoresByWeekQueryHandler(
        ILogger<GetLeagueScoresByWeekQueryHandler> logger,
        AppDataContext dbContext,
        ILeagueMembershipGuard membershipGuard)
    {
        _logger = logger;
        _dbContext = dbContext;
        _membershipGuard = membershipGuard;
    }

    public async Task<Result<LeagueScoresByWeekDto>> ExecuteAsync(
        GetLeagueScoresByWeekQuery query,
        CancellationToken cancellationToken = default)
    {
        // Per-member weekly scores — members only.
        // See docs/audit/league-authorization-idor.md.
        if (!await _membershipGuard.IsMemberAsync(query.LeagueId, query.UserId, cancellationToken))
        {
            return new Failure<LeagueScoresByWeekDto>(
                default!,
                ResultStatus.Forbid,
                [new ValidationFailure(nameof(query.LeagueId), "You are not a member of this league.")]);
        }

        var league = await _dbContext.PickemGroups
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == query.LeagueId, cancellationToken);

        if (league is null)
            return new Failure<LeagueScoresByWeekDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.LeagueId), $"League with ID {query.LeagueId} not found.")]);

        // Player Pick'em leagues have no pick results: their weeks come from
        // the members' lineup totals.
        if (league.GroupType == GroupType.PlayerPickem)
        {
            return new Success<LeagueScoresByWeekDto>(
                await BuildPlayerPickemScoresAsync(league, cancellationToken));
        }

        var weekResults = await _dbContext.PickemGroupWeekResults
            .AsNoTracking()
            .Include(r => r.User)
            .Where(r => r.PickemGroupId == query.LeagueId)
            .OrderBy(r => r.SeasonWeek)
            .ThenByDescending(r => r.TotalPoints)
            .ToListAsync(cancellationToken);

        if (weekResults.Count == 0)
        {
            _logger.LogWarning(
                "No week results found for leagueId={LeagueId}. Results may not have been calculated yet.",
                query.LeagueId);

            return new Success<LeagueScoresByWeekDto>(new LeagueScoresByWeekDto
            {
                LeagueId = query.LeagueId,
                LeagueName = league.Name,
                GroupType = league.GroupType.ToString(),
                Weeks = []
            });
        }

        var result = new LeagueScoresByWeekDto
        {
            LeagueId = query.LeagueId,
            LeagueName = league.Name,
            GroupType = league.GroupType.ToString(),
            Weeks = weekResults
                .GroupBy(r => r.SeasonWeek)
                .Select(g => new LeagueScoresByWeekDto.LeagueScoreByWeek
                {
                    WeekNumber = g.Key,
                    PickCount = g.First().TotalPicks,
                    UserScores = g.Select(r => new LeagueScoresByWeekDto.LeagueUserScoreDto
                    {
                        UserId = r.UserId,
                        UserName = r.User?.DisplayName ?? "Unknown",
                        IsSynthetic = r.User?.IsSynthetic ?? false,
                        WeekNumber = r.SeasonWeek,
                        PickCount = r.TotalPicks,
                        Score = r.TotalPoints,
                        IsDropWeek = r.IsDropWeek,
                        IsWeeklyWinner = r.IsWeeklyWinner,
                        Rank = r.Rank
                    }).ToList()
                }).ToList()
        };

        _logger.LogInformation(
            "Retrieved scores for {WeekCount} weeks for leagueId={LeagueId}",
            result.Weeks.Count,
            query.LeagueId);

        return new Success<LeagueScoresByWeekDto>(result);
    }

    /// <summary>
    /// Player Pick'em weeks from persisted lineup totals (the scoring
    /// consumers keep them fresh). Weekly winner = the week's top non-zero
    /// total, ties included, as in the Player Pick'em standings; rank is by
    /// points. Score/PickCount mirror Points (rounded) and PlayerCount so a
    /// client that doesn't branch on GroupType yet still shows something.
    /// </summary>
    private async Task<LeagueScoresByWeekDto> BuildPlayerPickemScoresAsync(
        PickemGroup league,
        CancellationToken cancellationToken)
    {
        var lineups = await (
                from l in _dbContext.PlayerLineups.AsNoTracking()
                join u in _dbContext.Users.AsNoTracking() on l.UserId equals u.Id
                where l.PickemGroupId == league.Id && l.SeasonYear == league.SeasonYear
                select new
                {
                    l.UserId,
                    u.DisplayName,
                    u.IsSynthetic,
                    l.SeasonWeek,
                    l.TotalPoints,
                    PlayerCount = l.Slots.Count,
                })
            .ToListAsync(cancellationToken);

        var weeks = lineups
            .GroupBy(l => l.SeasonWeek)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var top = g.Max(l => l.TotalPoints);
                var ordered = g.OrderByDescending(l => l.TotalPoints).ToList();
                return new LeagueScoresByWeekDto.LeagueScoreByWeek
                {
                    WeekNumber = g.Key,
                    PickCount = ordered.Count == 0 ? 0 : ordered.Max(l => l.PlayerCount),
                    UserScores = ordered.Select(l => new LeagueScoresByWeekDto.LeagueUserScoreDto
                    {
                        UserId = l.UserId,
                        UserName = l.DisplayName,
                        IsSynthetic = l.IsSynthetic,
                        WeekNumber = l.SeasonWeek,
                        PickCount = l.PlayerCount,
                        Score = (int)Math.Round(l.TotalPoints, MidpointRounding.AwayFromZero),
                        IsDropWeek = false,
                        IsWeeklyWinner = top > 0 && l.TotalPoints == top,
                        // Competition rank: ties share a rank, the next skips.
                        Rank = 1 + ordered.Count(o => o.TotalPoints > l.TotalPoints),
                        Points = l.TotalPoints,
                        PlayerCount = l.PlayerCount,
                    }).ToList(),
                };
            })
            .ToList();

        return new LeagueScoresByWeekDto
        {
            LeagueId = league.Id,
            LeagueName = league.Name,
            GroupType = league.GroupType.ToString(),
            Weeks = weeks,
        };
    }
}
