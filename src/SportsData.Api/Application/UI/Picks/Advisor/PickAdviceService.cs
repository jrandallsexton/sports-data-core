using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Application.UI.Picks.Advisor.Dtos;
using SportsData.Api.Application.UI.Picks.Advisor.Planner;
using SportsData.Api.Extensions;
using SportsData.Api.Infrastructure.Data;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Clients.Season;

namespace SportsData.Api.Application.UI.Picks.Advisor;

/// <summary>
/// Composes StatBot's advice for a league-week: loads the slate (with the
/// model's numbers and the preview's side), the leaderboard, scored
/// past-week results, and the CALLER's own picks, then runs
/// <see cref="IPickAdvisorPlanner"/>.
/// <para>
/// Rule zero: this service never reads another member's picks. The only
/// <c>UserPicks</c> query here is filtered to the caller. Standings come
/// from the leaderboard handler, which aggregates scored points only.
/// See docs/features/statbot-advisor.md.
/// </para>
/// </summary>
public interface IPickAdviceService
{
    Task<Result<PickAdviceDto>> BuildAsync(
        Guid userId,
        Guid leagueId,
        int week,
        AdvisorLevel? level,
        CancellationToken cancellationToken = default);
}

public class PickAdviceService : IPickAdviceService
{
    private readonly AppDataContext _dataContext;
    private readonly ILeagueMembershipGuard _membershipGuard;
    private readonly IGetLeagueWeekMatchupsQueryHandler _matchups;
    private readonly IGetLeaderboardQueryHandler _leaderboard;
    private readonly IPickAdvisorPlanner _planner;
    private readonly IDateTimeProvider _dateTime;
    private readonly ISeasonClientFactory _seasonClientFactory;

    public PickAdviceService(
        AppDataContext dataContext,
        ILeagueMembershipGuard membershipGuard,
        IGetLeagueWeekMatchupsQueryHandler matchups,
        IGetLeaderboardQueryHandler leaderboard,
        IPickAdvisorPlanner planner,
        IDateTimeProvider dateTime,
        ISeasonClientFactory seasonClientFactory)
    {
        _dataContext = dataContext;
        _membershipGuard = membershipGuard;
        _matchups = matchups;
        _leaderboard = leaderboard;
        _planner = planner;
        _dateTime = dateTime;
        _seasonClientFactory = seasonClientFactory;
    }

    public async Task<Result<PickAdviceDto>> BuildAsync(
        Guid userId,
        Guid leagueId,
        int week,
        AdvisorLevel? level,
        CancellationToken cancellationToken = default)
    {
        if (leagueId == Guid.Empty)
            return Fail(ResultStatus.Validation, nameof(leagueId), "League ID cannot be empty.");

        if (level is not null && !Enum.IsDefined(level.Value))
            return Fail(ResultStatus.Validation, nameof(level), "Unknown advisor level.");

        if (!await _membershipGuard.IsMemberAsync(leagueId, userId, cancellationToken))
            return Fail(ResultStatus.Forbid, nameof(leagueId), "You are not a member of this league.");

        var group = await _dataContext.PickemGroups
            .AsNoTracking()
            .Where(g => g.Id == leagueId)
            .Select(g => new { g.Id, g.PickType, g.UseConfidencePoints, g.SeasonYear, g.DeactivatedUtc, g.Sport })
            .FirstOrDefaultAsync(cancellationToken);

        if (group is null)
            return Fail(ResultStatus.NotFound, nameof(leagueId), "League not found.");

        if (group.DeactivatedUtc is not null)
            return Fail(ResultStatus.Validation, nameof(leagueId), "This league has ended.");

        var slate = await _matchups.ExecuteAsync(
            new GetLeagueWeekMatchupsQuery { UserId = userId, LeagueId = leagueId, Week = week },
            cancellationToken);

        if (!slate.IsSuccess)
            return Propagate(slate);

        var standings = await _leaderboard.ExecuteAsync(
            new GetLeaderboardQuery { GroupId = leagueId, UserId = userId },
            cancellationToken);

        if (!standings.IsSuccess)
            return Propagate(standings);

        var nowUtc = _dateTime.UtcNow();

        // The ONLY picks query. Caller-scoped by construction.
        var ownPicks = await _dataContext.UserPicks
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.PickemGroupId == leagueId && p.Week == week)
            .Select(p => new { p.ContestId, p.FranchiseSeasonId, p.ConfidencePoints })
            .ToListAsync(cancellationToken);

        var ownByContest = ownPicks
            .GroupBy(p => p.ContestId)
            .ToDictionary(g => g.Key, g => new PickAdvisorExistingPick(g.First().FranchiseSeasonId, g.First().ConfidencePoints));

        // Horizon. Only THIS week's slate is a known quantity: matchups sync
        // about a week ahead, and a ranked-teams league (AP Top 25 + a
        // conference) can't have its future slates forecast at all. So no
        // game counts beyond this week, ever. Weeks come from the season
        // calendar (Season service), not the league's own week rows — those
        // are only created as matchups sync, which is what made a
        // full-season league read "1 week left" (2026-09-25). Regular-season
        // weeks whose end is still ahead; null when the calendar is
        // unreachable, so the copy can omit it rather than guess.
        var regularSeasonWeeksLeft = await RegularSeasonWeeksLeftAsync(group.Sport, group.SeasonYear, nowUtc, cancellationToken);

        // This week's facts: open games, and the most points they can yield.
        // In a confidence league the values are 1..N over the whole slate;
        // whatever the caller's LOCKED picks already hold is off the table.
        var lockedContests = slate.Value.Matchups
            .Where(m => PickemGroupMatchupExtensions.IsStartLocked(m.StartDateUtc, nowUtc))
            .Select(m => m.ContestId)
            .ToHashSet();
        var gamesThisWeek = slate.Value.Matchups.Count - lockedContests.Count;
        var slateSize = slate.Value.Matchups.Count;
        var reservedPoints = ownPicks
            .Where(p => lockedContests.Contains(p.ContestId) && p.ConfidencePoints.HasValue)
            .Sum(p => p.ConfidencePoints!.Value);
        var maxPointsThisWeek = group.UseConfidencePoints
            ? Math.Max(0, slateSize * (slateSize + 1) / 2 - reservedPoints)
            : gamesThisWeek;

        // Spread of per-game scoring across scored member-weeks — points and
        // pick counts, never which picks.
        var perGameRates = await _dataContext.PickemGroupWeekResults
            .AsNoTracking()
            .Where(r => r.PickemGroupId == leagueId && r.SeasonYear == group.SeasonYear && r.TotalPicks > 0)
            .Select(r => new { r.TotalPoints, r.TotalPicks })
            .ToListAsync(cancellationToken);

        var pointsPerGameStdDev = StdDev(perGameRates.Select(r => (double)r.TotalPoints / r.TotalPicks).ToList());

        var board = standings.Value;
        var me = board.FirstOrDefault(x => x.UserId == userId);
        var leader = board.OrderBy(x => x.Rank).FirstOrDefault();
        var statBot = board.FirstOrDefault(x => x.UserId == IStatBotPickWriter.StatBotUserId);

        var myTotal = me?.TotalPoints ?? 0;
        var leaderTotal = leader?.TotalPoints ?? 0;
        var deficit = leaderTotal - myTotal;

        // Something actually reachable, from the standings alone. The people
        // ahead don't score zero this week: assume each keeps their pace
        // (points per game × this week's games). A member is passable when
        // my total plus this week's maximum beats their total plus their
        // expected week. "A perfect week takes the lead" with the leader at
        // zero was the earlier, wrong version (2026-09-25).
        int ExpectedThisWeek(decimal pointsPerGame) =>
            (int)Math.Round(pointsPerGame * gamesThisWeek, MidpointRounding.AwayFromZero);

        var leaderExpected = ExpectedThisWeek(leader?.PointsPerGame ?? 0);
        var ahead = me is null
            ? null
            : board.Where(x => x.Rank < me.Rank).OrderByDescending(x => x.Rank).ThenBy(x => x.Name).FirstOrDefault();
        var aheadExpected = ahead is null ? (int?)null : ExpectedThisWeek(ahead.PointsPerGame);
        var bestCaseRank = me is null
            ? (int?)null
            : 1 + board.Count(x => x.UserId != me.UserId
                                   && x.TotalPoints + ExpectedThisWeek(x.PointsPerGame) >= myTotal + maxPointsThisWeek);

        var recommended = _planner.RecommendLevel(new PickAdvisorStandings(
            deficit, Math.Max(1, regularSeasonWeeksLeft ?? 1), gamesThisWeek, pointsPerGameStdDev, leader?.PointsPerGame ?? 0));

        var applied = level ?? recommended;

        var matchups = slate.Value.Matchups
            .Select(m =>
            {
                var prediction = m.Predictions.FirstOrDefault(p => p.PredictionType == group.PickType);
                return new PickAdvisorMatchup(
                    m.ContestId,
                    m.HeadLine,
                    m.StartDateUtc,
                    PickemGroupMatchupExtensions.IsStartLocked(m.StartDateUtc, nowUtc),
                    m.HomeFranchiseSeasonId,
                    m.AwayFranchiseSeasonId,
                    prediction is null ? null : new PickAdvisorSignal(prediction.WinnerFranchiseSeasonId, (double)prediction.WinProbability),
                    m.AiWinnerFranchiseSeasonId,
                    ownByContest.GetValueOrDefault(m.ContestId));
            })
            .ToList();

        var sheet = _planner.BuildSheet(new PickAdvisorPlanInput(applied, group.UseConfidencePoints, matchups));

        var dto = new PickAdviceDto
        {
            LeagueId = leagueId,
            Week = week,
            PickType = group.PickType,
            UseConfidencePoints = group.UseConfidencePoints,
            RecommendedLevel = recommended,
            Level = applied,
            Analysis = new PickAdviceAnalysisDto
            {
                Rank = me?.Rank,
                LastWeekRank = me?.LastWeekRank,
                MemberCount = board.Count,
                TotalPoints = myTotal,
                WeeklyAverage = me?.WeeklyAverage ?? 0,
                PointsPerGame = me?.PointsPerGame ?? 0,
                PickAccuracy = me?.PickAccuracy ?? 0,
                LeaderName = leader?.Name,
                LeaderTotalPoints = leaderTotal,
                LeaderWeeklyAverage = leader?.WeeklyAverage ?? 0,
                LeaderPointsPerGame = leader?.PointsPerGame ?? 0,
                Deficit = deficit,
                RegularSeasonWeeksLeft = regularSeasonWeeksLeft,
                GamesThisWeek = gamesThisWeek,
                MaxPointsThisWeek = maxPointsThisWeek,
                LeaderExpectedThisWeek = leaderExpected,
                CanCloseGapThisWeek = deficit > 0 && deficit + leaderExpected < maxPointsThisWeek,
                BestCaseRankThisWeek = bestCaseRank,
                NextAheadName = ahead?.Name,
                NextAheadRank = ahead?.Rank,
                PointsBehindNextAhead = ahead is null ? null : ahead.TotalPoints - myTotal,
                NextAheadExpectedThisWeek = aheadExpected,
                PointsPerGameStdDev = pointsPerGameStdDev is null ? null : Math.Round(pointsPerGameStdDev.Value, 3),
                StatBot = statBot is null ? null : new PickAdviceStatBotDto
                {
                    Rank = statBot.Rank,
                    TotalPoints = statBot.TotalPoints,
                    WeeklyAverage = statBot.WeeklyAverage,
                    PointsPerGame = statBot.PointsPerGame
                }
            },
            Picks = sheet.Picks.Select(p => new AdvisedPickDto
            {
                ContestId = p.ContestId,
                Headline = p.Headline,
                Kind = p.Kind,
                FranchiseSeasonId = p.FranchiseSeasonId,
                ConfidencePoints = p.ConfidencePoints,
                ModelProbability = p.ModelProbability,
                PreviewAgrees = p.PreviewAgrees,
                IsCoinFlip = p.IsCoinFlip,
                DiffersFromExisting = p.DiffersFromExisting
            }).ToList(),
            FlipCount = sheet.FlipCount,
            CoinFlipCount = sheet.CoinFlipCount,
            LockedCount = sheet.LockedCount,
            NoPredictionCount = sheet.NoPredictionCount
        };

        return new Success<PickAdviceDto>(dto);
    }

    /// <summary>
    /// Regular-season weeks on the season calendar whose end is still ahead.
    /// Null when the Season service can't answer — never a guess.
    /// </summary>
    private async Task<int?> RegularSeasonWeeksLeftAsync(Sport sport, int seasonYear, DateTime nowUtc, CancellationToken ct)
    {
        try
        {
            var overview = await _seasonClientFactory.Resolve(sport).GetSeasonOverview(seasonYear, ct);
            if (!overview.IsSuccess || overview.Value is null) return null;

            return overview.Value.Weeks.Count(w =>
                w.SeasonPhaseName.Contains("regular", StringComparison.OrdinalIgnoreCase)
                && w.EndDate > nowUtc);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>Population standard deviation; null below two samples.</summary>
    private static double? StdDev(IReadOnlyCollection<double> values)
    {
        if (values.Count < 2) return null;
        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return Math.Sqrt(variance);
    }

    private static Failure<PickAdviceDto> Fail(ResultStatus status, string field, string message) =>
        new(default!, status, [new ValidationFailure(field, message)]);

    /// <summary>Carries an upstream handler's status and errors through unchanged.</summary>
    private static Failure<PickAdviceDto> Propagate<T>(Result<T> upstream) =>
        new(default!, upstream.Status, (upstream as Failure<T>)?.Errors ?? []);
}
