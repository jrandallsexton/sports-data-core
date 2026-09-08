using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Picks.Dtos;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Contest;

namespace SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekOverview;

public interface IGetLeagueWeekOverviewQueryHandler
{
    Task<Result<LeagueWeekOverviewDto>> ExecuteAsync(
        GetLeagueWeekOverviewQuery query,
        CancellationToken cancellationToken = default);
}

public class GetLeagueWeekOverviewQueryHandler : IGetLeagueWeekOverviewQueryHandler
{
    private readonly ILogger<GetLeagueWeekOverviewQueryHandler> _logger;
    private readonly AppDataContext _dbContext;
    private readonly IContestClientFactory _contestClientFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILeagueMembershipGuard _membershipGuard;

    public GetLeagueWeekOverviewQueryHandler(
        ILogger<GetLeagueWeekOverviewQueryHandler> logger,
        AppDataContext dbContext,
        IContestClientFactory contestClientFactory,
        IDateTimeProvider dateTimeProvider,
        ILeagueMembershipGuard membershipGuard)
    {
        _logger = logger;
        _dbContext = dbContext;
        _contestClientFactory = contestClientFactory;
        _dateTimeProvider = dateTimeProvider;
        _membershipGuard = membershipGuard;
    }

    public async Task<Result<LeagueWeekOverviewDto>> ExecuteAsync(
        GetLeagueWeekOverviewQuery query,
        CancellationToken cancellationToken = default)
    {
        // Every member's picks for the week — members only.
        // See docs/audit/league-authorization-idor.md.
        if (!await _membershipGuard.IsMemberAsync(query.LeagueId, query.UserId, cancellationToken))
        {
            return new Failure<LeagueWeekOverviewDto>(
                default!,
                ResultStatus.Forbid,
                [new ValidationFailure(nameof(query.LeagueId), "You are not a member of this league.")]);
        }

        var league = await _dbContext.PickemGroups
            .AsNoTracking()
            .Include(x => x.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(g => g.Id == query.LeagueId, cancellationToken);

        if (league is null)
            return new Failure<LeagueWeekOverviewDto>(
                default!,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(query.LeagueId), $"League with ID {query.LeagueId} not found.")]);

        var matchups = await _dbContext.PickemGroupMatchups
            .AsNoTracking()
            .Where(m => m.GroupId == query.LeagueId && m.SeasonWeek == query.Week)
            .ToListAsync(cancellationToken);

        var contestIds = matchups
            .Select(m => m.ContestId)
            .ToList();

        var result = new LeagueWeekOverviewDto();

        var contestResultsResponse = await _contestClientFactory.Resolve(league.Sport)
            .GetContestResultsByContestIds(contestIds);
        if (!contestResultsResponse.IsSuccess)
        {
            return new Failure<LeagueWeekOverviewDto>(
                default!,
                ResultStatus.Error,
                [new ValidationFailure("contests", "Failed to retrieve contest results from Producer")]);
        }
        var canonicalContests = contestResultsResponse.Value;

        var now = _dateTimeProvider.UtcNow();
        var lockedContestIds = new HashSet<Guid>();
        var canonicalContestIds = new HashSet<Guid>();

        foreach (var canonicalContest in canonicalContests)
        {
            var matchup = matchups
                .FirstOrDefault(m => m.ContestId == canonicalContest.ContestId);

            if (matchup is null)
            {
                _logger.LogError("Matchup could not be found for contest {ContestId}", canonicalContest.ContestId);
                return new Failure<LeagueWeekOverviewDto>(
                    default!,
                    ResultStatus.BadRequest,
                    [new ValidationFailure(nameof(canonicalContest.ContestId), "Matchup could not be found")]);
            }

            canonicalContestIds.Add(canonicalContest.ContestId);

            canonicalContest.IsLocked = canonicalContest.StartDateUtc.AddMinutes(-5) <= now;
            if (canonicalContest.IsLocked)
                lockedContestIds.Add(canonicalContest.ContestId);

            canonicalContest.WinnerFranchiseSeasonId = canonicalContest.AwayScore > canonicalContest.HomeScore
                ? canonicalContest.AwayFranchiseSeasonId
                : canonicalContest.HomeScore > canonicalContest.AwayScore
                    ? canonicalContest.HomeFranchiseSeasonId
                    : null; // Tie - no winner

            // Determine spread winner based on the matchup spread
            if (matchup is { AwaySpread: not null, HomeSpread: not null })
            {
                var spreadDifference = (canonicalContest.AwayScore + matchup.AwaySpread.Value) - canonicalContest.HomeScore;
                if (spreadDifference > 0)
                    canonicalContest.SpreadWinnerFranchiseSeasonId = canonicalContest.AwayFranchiseSeasonId;
                else if (spreadDifference < 0)
                    canonicalContest.SpreadWinnerFranchiseSeasonId = canonicalContest.HomeFranchiseSeasonId;
                else
                    canonicalContest.SpreadWinnerFranchiseSeasonId = null; // Push
            }
            else
            {
                canonicalContest.SpreadWinnerFranchiseSeasonId = null; // No spread
            }
        }

        result.Contests = canonicalContests.OrderBy(x => x.StartDateUtc)
            .Select(x => new LeagueWeekMatchupResultDto(x)
            {
                LeagueWinnerFranchiseSeasonId = x.SpreadWinnerFranchiseSeasonId ?? x.WinnerFranchiseSeasonId
            }).ToList();

        var memberIds = league.Members.Select(m => m.UserId).ToList();

        // Readiness counts: how many of the week's games each member has
        // picked, INCLUDING picks on un-locked contests — a count is safe
        // metadata (it says nothing about what was picked) and is the only
        // pre-lock signal the "Who's Ready" list needs. Scoped to the
        // canonical contest list so stale picks can't inflate an X/Y readout
        // past Y.
        var submittedCounts = await _dbContext.UserPicks
            .AsNoTracking()
            .Where(p =>
                p.PickemGroupId == query.LeagueId &&
                p.Week == query.Week &&
                memberIds.Contains(p.UserId) &&
                canonicalContestIds.Contains(p.ContestId))
            .GroupBy(p => p.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        // The member roster rides on the DTO so renderers can derive matrix
        // columns from it — a picks-derived column set would drop members
        // whose picks are all withheld (un-locked) mid-week.
        result.Members = league.Members
            .OrderBy(m => m.User.DisplayName)
            .Select(m => new LeagueWeekMemberDto
            {
                UserId = m.UserId,
                DisplayName = m.User.DisplayName,
                IsSynthetic = m.User.IsSynthetic,
                SubmittedPickCount = submittedCounts.GetValueOrDefault(m.UserId)
            })
            .ToList();

        // REVEAL ENFORCEMENT (server-side): another member's pick is visible
        // only once its contest has locked (kickoff − 5 min — the same rule
        // that stamps IsLocked above). The caller always sees their own
        // picks. Before this filter, the endpoint returned every member's
        // picks for the whole week and relied on the web renderer to hide
        // unlocked rows — i.e. any member could read the league's un-locked
        // picks out of the payload. Fail-closed both ways: every pick —
        // the caller's included — must belong to this week's canonical
        // contest list, and others' additionally to its locked subset.
        //
        // One set-based query for the whole league also replaces the
        // previous per-member handler loop (3 queries per member — the N+1
        // called out in docs/audit/launch-readiness-2026-07.md).
        result.UserPicks = await _dbContext.UserPicks
            .AsNoTracking()
            .Where(p =>
                p.PickemGroupId == query.LeagueId &&
                p.Week == query.Week &&
                memberIds.Contains(p.UserId) &&
                canonicalContestIds.Contains(p.ContestId) &&
                (p.UserId == query.UserId || lockedContestIds.Contains(p.ContestId)))
            .OrderBy(p => p.User.DisplayName)
            .Select(p => new UserPickDto
            {
                Id = p.Id,
                UserId = p.UserId,
                User = p.User.DisplayName,
                ConfidencePoints = p.ConfidencePoints,
                ContestId = p.ContestId,
                FranchiseSeasonId = p.FranchiseSeasonId ?? Guid.Empty,
                IsCorrect = p.IsCorrect,
                ScoredAt = p.ScoredAt,
                PickType = p.PickType,
                TiebreakerGuessTotal = p.TiebreakerGuessTotal,
                PointsAwarded = p.PointsAwarded,
                IsSynthetic = p.User.IsSynthetic
            })
            .ToListAsync(cancellationToken);

        return new Success<LeagueWeekOverviewDto>(result);
    }
}
