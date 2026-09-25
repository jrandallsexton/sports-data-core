using FluentAssertions;

using Microsoft.Extensions.Logging;

using Moq;

using SportsData.Api.Application.Admin.SyntheticPicks;
using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.UI.Contest.Dtos;
using SportsData.Api.Application.UI.Leaderboard.Queries.GetLeaderboard;
using SportsData.Api.Application.UI.Leagues.Authorization;
using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Application.UI.Picks.Advisor;
using SportsData.Api.Application.UI.Picks.Advisor.Planner;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;

using Xunit;

using UserEntity = SportsData.Api.Infrastructure.Data.Entities.User;

namespace SportsData.Api.Tests.Unit.Application.UI.Picks.Advisor;

/// <summary>
/// Composition tests. Standings come from the REAL leaderboard handler over
/// seeded scored picks; the slate comes from a mocked matchups handler
/// (that handler talks to Producer). The blindness test is the one that
/// pins rule zero: see docs/features/statbot-advisor.md.
/// </summary>
public class PickAdviceServiceTests : ApiTestBase<PickAdviceService>
{
    private static readonly DateTime NowUtc = new(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);
    private const int CurrentWeek = 3;
    private const int SeasonYear = 2026;

    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _leader = Guid.NewGuid();
    private readonly Guid _rival = Guid.NewGuid();
    private readonly Guid _statBot = IStatBotPickWriter.StatBotUserId;

    private readonly Mock<IGetLeagueWeekMatchupsQueryHandler> _matchups = new();
    private readonly Mock<IProvideSeasons> _seasons = new();

    public PickAdviceServiceTests()
    {
        // Season calendar: 12 regular-season weeks, 3 already over (week 4
        // ends two days from now, so it counts), plus a postseason week that
        // must not count. Nine regular-season weeks left.
        var weeks = Enumerable.Range(1, 12).Select(n => new SeasonWeekDto
        {
            Id = Guid.NewGuid(), Number = n, Label = $"Week {n}", SeasonPhaseName = "Regular Season",
            StartDate = NowUtc.AddDays((n - 4) * 7 - 5), EndDate = NowUtc.AddDays((n - 4) * 7 + 2)
        }).ToList();
        weeks.Add(new SeasonWeekDto
        {
            Id = Guid.NewGuid(), Number = 1, Label = "Bowls", SeasonPhaseName = "Postseason",
            StartDate = NowUtc.AddDays(70), EndDate = NowUtc.AddDays(100)
        });
        _seasons
            .Setup(x => x.GetSeasonOverview(SeasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<SeasonOverviewDto>(new SeasonOverviewDto { SeasonYear = SeasonYear, Weeks = weeks }));
    }

    private PickAdviceService CreateService()
    {
        var dateTime = new Mock<IDateTimeProvider>();
        dateTime.Setup(x => x.UtcNow()).Returns(NowUtc);

        var guard = Mocker.Get<ILeagueMembershipGuard>();
        var leaderboard = new GetLeaderboardQueryHandler(
            Mocker.Get<ILogger<GetLeaderboardQueryHandler>>(), DataContext, guard);

        var factory = new Mock<ISeasonClientFactory>();
        factory.Setup(x => x.Resolve(It.IsAny<Sport>())).Returns(_seasons.Object);

        return new PickAdviceService(
            DataContext, guard, _matchups.Object, leaderboard,
            new PickAdvisorPlanner(new PickAdvisorOptions()), dateTime.Object, factory.Object);
    }

    // ── Seeding ────────────────────────────────────────────────────────────

    private Guid SeedLeague(PickType pickType = PickType.StraightUp, bool useConfidence = true, bool deactivated = false)
    {
        var groupId = Guid.NewGuid();
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = groupId,
            Name = "Advice League",
            Sport = Sport.FootballNcaa,
            League = League.NCAAF,
            PickType = pickType,
            UseConfidencePoints = useConfidence,
            SeasonYear = SeasonYear,
            DeactivatedUtc = deactivated ? NowUtc : null,
            CommissionerUserId = _me,
            CreatedUtc = NowUtc,
            CreatedBy = _me
        });

        foreach (var (id, name, synthetic) in new[] { (_me, "Me", false), (_leader, "Leader", false), (_rival, "Rival", false), (_statBot, "StatBot", true) })
        {
            DataContext.Users.Add(new UserEntity
            {
                Id = id,
                FirebaseUid = $"fb-{id:N}",
                Email = $"{id:N}@example.com",
                SignInProvider = "test",
                DisplayName = name,
                Username = $"u{id:N}"[..12],
                IsSynthetic = synthetic,
                LastLoginUtc = NowUtc
            });
            DataContext.PickemGroupMembers.Add(new PickemGroupMember
            {
                Id = Guid.NewGuid(), PickemGroupId = groupId, UserId = id, Role = LeagueRole.Member
            });
        }

        // Weeks 1–2 are done, week 3 (current) has games left. Only synced
        // weeks exist here — the horizon comes from the season calendar.
        SeedGroupMatchup(groupId, 1, NowUtc.AddDays(-14));
        SeedGroupMatchup(groupId, 2, NowUtc.AddDays(-7));
        SeedGroupMatchup(groupId, 3, NowUtc.AddDays(-1));
        SeedGroupMatchup(groupId, 3, NowUtc.AddDays(2));

        return groupId;
    }

    private void SeedGroupMatchup(Guid groupId, int week, DateTime startUtc)
    {
        DataContext.PickemGroupMatchups.Add(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            SeasonWeekId = WeekId(week),
            ContestId = Guid.NewGuid(),
            StartDateUtc = startUtc,
            SeasonYear = SeasonYear,
            SeasonWeek = week,
            Headline = "Away @ Home"
        });
    }

    private static Guid WeekId(int week) => new(week, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Scored picks for weeks 1–2 → leaderboard: leader 20, StatBot 14, rival 12, me 10.</summary>
    private void SeedStandings(Guid groupId)
    {
        SeedScored(groupId, _leader, week: 1, points: 10);
        SeedScored(groupId, _leader, week: 2, points: 10);
        SeedScored(groupId, _statBot, week: 1, points: 7);
        SeedScored(groupId, _statBot, week: 2, points: 7);
        SeedScored(groupId, _rival, week: 1, points: 6);
        SeedScored(groupId, _rival, week: 2, points: 6);
        SeedScored(groupId, _me, week: 1, points: 4);
        SeedScored(groupId, _me, week: 2, points: 6);

        foreach (var (user, w, pts) in new[] { (_leader, 1, 10), (_leader, 2, 10), (_statBot, 1, 7), (_statBot, 2, 7), (_rival, 1, 6), (_rival, 2, 6), (_me, 1, 4), (_me, 2, 6) })
        {
            DataContext.PickemGroupWeekResults.Add(new PickemGroupWeekResult
            {
                Id = Guid.NewGuid(), PickemGroupId = groupId, UserId = user, SeasonYear = SeasonYear, SeasonWeek = w,
                TotalPoints = pts, CorrectPicks = pts, TotalPicks = 10, CalculatedUtc = NowUtc
            });
        }
    }

    private void SeedScored(Guid groupId, Guid userId, int week, int points)
    {
        DataContext.UserPicks.Add(new PickemGroupUserPick
        {
            Id = Guid.NewGuid(), PickemGroupId = groupId, UserId = userId, ContestId = Guid.NewGuid(), Week = week,
            PickType = PickType.StraightUp, FranchiseSeasonId = Guid.NewGuid(), ConfidencePoints = points,
            IsCorrect = true, PointsAwarded = points, ScoredAt = NowUtc.AddDays(-3), TiebreakerType = TiebreakerType.TotalPoints
        });
    }

    private void SeedCurrentWeekPick(Guid groupId, Guid userId, Guid contestId, Guid side, int? points)
    {
        DataContext.UserPicks.Add(new PickemGroupUserPick
        {
            Id = Guid.NewGuid(), PickemGroupId = groupId, UserId = userId, ContestId = contestId, Week = CurrentWeek,
            PickType = PickType.StraightUp, FranchiseSeasonId = side, ConfidencePoints = points,
            TiebreakerType = TiebreakerType.TotalPoints
        });
    }

    private sealed record Slate(Guid ContestId, Guid Home, Guid Away, double PHome);

    private static Slate[] ThreeGames() =>
    [
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.85),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.65),
        new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.52)
    ];

    private void MockSlate(PickType pickType, bool useConfidence, params Slate[] games)
    {
        var dto = new LeagueWeekMatchupsDto
        {
            SeasonYear = SeasonYear,
            WeekNumber = CurrentWeek,
            PickType = pickType,
            UseConfidencePoints = useConfidence,
            Sport = "FootballNcaa",
            Matchups = games.Select(g => new LeagueWeekMatchupsDto.MatchupForPickDto
            {
                ContestId = g.ContestId,
                HeadLine = "Away @ Home",
                StartDateUtc = NowUtc.AddDays(2),
                HomeFranchiseSeasonId = g.Home,
                AwayFranchiseSeasonId = g.Away,
                AiWinnerFranchiseSeasonId = g.PHome >= 0.5 ? g.Home : g.Away,
                Predictions =
                [
                    new ContestPredictionDto { ContestId = g.ContestId, WinnerFranchiseSeasonId = g.Home, WinProbability = (decimal)g.PHome, PredictionType = pickType, ModelVersion = "test" },
                    // The other type's number must be ignored.
                    new ContestPredictionDto { ContestId = g.ContestId, WinnerFranchiseSeasonId = g.Away, WinProbability = 0.99m, PredictionType = pickType == PickType.StraightUp ? PickType.AgainstTheSpread : PickType.StraightUp, ModelVersion = "test" }
                ]
            }).ToList()
        };

        _matchups
            .Setup(x => x.ExecuteAsync(It.Is<GetLeagueWeekMatchupsQuery>(q => q.Week == CurrentWeek), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<LeagueWeekMatchupsDto>(dto));
    }

    // ── Tests ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task NotAMember_IsForbidden()
    {
        var groupId = SeedLeague();
        await DataContext.SaveChangesAsync();
        DenyLeagueMembership();

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, null);

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.Forbid);
    }

    [Fact]
    public async Task UnknownLeague_IsNotFound()
    {
        var result = await CreateService().BuildAsync(_me, Guid.NewGuid(), CurrentWeek, null);

        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task EndedLeague_IsRejected()
    {
        var groupId = SeedLeague(deactivated: true);
        await DataContext.SaveChangesAsync();

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, null);

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task UnknownLevel_IsRejected()
    {
        var groupId = SeedLeague();
        await DataContext.SaveChangesAsync();

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, (AdvisorLevel)42);

        result.Status.Should().Be(ResultStatus.Validation);
    }

    [Fact]
    public async Task Analysis_ComesFromStandingsAndPerformanceOnly()
    {
        var groupId = SeedLeague();
        SeedStandings(groupId);
        await DataContext.SaveChangesAsync();
        MockSlate(PickType.StraightUp, true, ThreeGames());

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, null);

        result.IsSuccess.Should().BeTrue();
        var a = result.Value.Analysis;
        a.Rank.Should().Be(4);
        a.MemberCount.Should().Be(4);
        a.TotalPoints.Should().Be(10);
        a.WeeklyAverage.Should().Be(5.0m);
        a.PointsPerGame.Should().Be(5.0m);          // 10 points over 2 decided picks
        a.LeaderName.Should().Be("Leader");
        a.LeaderTotalPoints.Should().Be(20);
        a.LeaderWeeklyAverage.Should().Be(10.0m);
        a.LeaderPointsPerGame.Should().Be(10.0m);
        a.Deficit.Should().Be(10);
        a.GamesThisWeek.Should().Be(3);             // the mocked slate, all unlocked
        a.RegularSeasonWeeksLeft.Should().Be(9);    // season calendar, not the league's synced week rows
        a.MaxPointsThisWeek.Should().Be(6);         // confidence values 1+2+3, nothing locked
        a.LeaderExpectedThisWeek.Should().Be(30);   // 10.0 per game × 3 games — the leader does not score zero
        a.CanCloseGapThisWeek.Should().BeFalse();   // 10 behind + 30 at pace, 6 on the table
        // At pace nobody above is passable: rival 12 + 18 = 30, StatBot 14 + 21 = 35,
        // leader 20 + 30 = 50, all ≥ my 10 + 6 → best case is where I am.
        a.BestCaseRankThisWeek.Should().Be(4);
        a.NextAheadName.Should().Be("Rival");
        a.NextAheadRank.Should().Be(3);
        a.PointsBehindNextAhead.Should().Be(2);
        a.NextAheadExpectedThisWeek.Should().Be(18);
        // Per-game rates from week results {1.0,1.0,.7,.7,.6,.6,.4,.6}: mean .7, variance .30/8
        a.PointsPerGameStdDev.Should().BeApproximately(0.194, 0.001);
        a.StatBot.Should().BeEquivalentTo(new { Rank = 2, TotalPoints = 14, WeeklyAverage = 7.0m, PointsPerGame = 7.0m });
    }

    [Fact]
    public async Task Level_DefaultsToTheRecommendation_AndHonoursAnExplicitOne()
    {
        var groupId = SeedLeague();
        SeedStandings(groupId);
        await DataContext.SaveChangesAsync();
        MockSlate(PickType.StraightUp, true, ThreeGames());
        var service = CreateService();

        // 10 over 9 weeks = 1.1/week; swing 0.194 × 3 games = 0.58 → 1.9 units → QB Draw
        var recommended = await service.BuildAsync(_me, groupId, CurrentWeek, null);
        recommended.Value.RecommendedLevel.Should().Be(AdvisorLevel.QbDraw);
        recommended.Value.Level.Should().Be(AdvisorLevel.QbDraw);
        recommended.Value.FlipCount.Should().Be(1);

        var explicitLevel = await service.BuildAsync(_me, groupId, CurrentWeek, AdvisorLevel.Prevent);
        explicitLevel.Value.RecommendedLevel.Should().Be(AdvisorLevel.QbDraw);
        explicitLevel.Value.Level.Should().Be(AdvisorLevel.Prevent);
        explicitLevel.Value.FlipCount.Should().Be(0);
    }

    [Fact]
    public async Task Sheet_UsesTheLeaguePickTypesNumber_AndTheCallersOwnPicks()
    {
        var groupId = SeedLeague();
        SeedStandings(groupId);
        var games = ThreeGames();
        // My existing pick on the surest game already matches what Prevent will advise (home, 3 points).
        SeedCurrentWeekPick(groupId, _me, games[0].ContestId, games[0].Home, 3);
        await DataContext.SaveChangesAsync();
        MockSlate(PickType.StraightUp, true, games);

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, AdvisorLevel.Prevent);

        result.IsSuccess.Should().BeTrue();
        result.Value.PickType.Should().Be(PickType.StraightUp);
        result.Value.UseConfidencePoints.Should().BeTrue();
        result.Value.Picks.Should().HaveCount(3);

        var surest = result.Value.Picks.Single(p => p.ContestId == games[0].ContestId);
        surest.FranchiseSeasonId.Should().Be(games[0].Home);
        surest.ModelProbability.Should().Be(0.85);   // not the 0.99 from the other pick type
        surest.ConfidencePoints.Should().Be(3);
        surest.DiffersFromExisting.Should().BeFalse();

        result.Value.Picks.Single(p => p.ContestId == games[2].ContestId).DiffersFromExisting.Should().BeTrue();
    }

    [Fact]
    public async Task RuleZero_OtherMembersCurrentWeekPicks_CannotChangeTheAdvice()
    {
        var groupId = SeedLeague();
        SeedStandings(groupId);
        var games = ThreeGames();
        SeedCurrentWeekPick(groupId, _me, games[1].ContestId, games[1].Away, 1);
        await DataContext.SaveChangesAsync();
        MockSlate(PickType.StraightUp, true, games);
        var service = CreateService();

        var before = await service.BuildAsync(_me, groupId, CurrentWeek, null);

        // Now every other member — human and bot — has a full, loud sheet this week.
        foreach (var other in new[] { _leader, _rival, _statBot })
        {
            for (var i = 0; i < games.Length; i++)
                SeedCurrentWeekPick(groupId, other, games[i].ContestId, games[i].Away, games.Length - i);
        }
        await DataContext.SaveChangesAsync();

        var after = await service.BuildAsync(_me, groupId, CurrentWeek, null);

        after.IsSuccess.Should().BeTrue();
        after.Value.Should().BeEquivalentTo(before.Value);
    }

    [Fact]
    public async Task NonConfidenceLeague_SheetCarriesNoPoints()
    {
        var groupId = SeedLeague(useConfidence: false);
        SeedStandings(groupId);
        await DataContext.SaveChangesAsync();
        MockSlate(PickType.StraightUp, false, ThreeGames());

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, AdvisorLevel.Prevent);

        result.Value.UseConfidencePoints.Should().BeFalse();
        result.Value.Picks.Should().OnlyContain(p => p.ConfidencePoints == null);
    }

    [Fact]
    public async Task NoScoredWeeksYet_StillAdvises_WithAnEmptyAnalysis()
    {
        var groupId = SeedLeague();
        await DataContext.SaveChangesAsync();
        MockSlate(PickType.StraightUp, true, ThreeGames());

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.RecommendedLevel.Should().Be(AdvisorLevel.Prevent);
        result.Value.Analysis.Rank.Should().BeNull();
        result.Value.Analysis.StatBot.Should().BeNull();
        result.Value.Analysis.PointsPerGameStdDev.Should().BeNull();
        result.Value.Picks.Should().HaveCount(3);
    }

    [Fact]
    public async Task Reachability_AccountsForTheOtherMembersPace()
    {
        // A big slate: 10 games → max 55 confidence points. Rival (12 total,
        // 6.0/game → 60 expected) stays ahead of my 10 + 55 = 65? No: 72 ≥ 65 →
        // not passable. StatBot (14, 7.0/game → 70 expected) 84 ≥ 65 → not
        // passable either. The leader (20, 10/game → 100) plainly not. So pace
        // matters: with everyone at zero all three would have been passable.
        var groupId = SeedLeague();
        SeedStandings(groupId);
        await DataContext.SaveChangesAsync();
        var games = Enumerable.Range(0, 10).Select(_ => new Slate(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0.7)).ToArray();
        MockSlate(PickType.StraightUp, true, games);

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, null);

        var a = result.Value.Analysis;
        a.MaxPointsThisWeek.Should().Be(55);
        a.CanCloseGapThisWeek.Should().BeFalse();
        a.BestCaseRankThisWeek.Should().Be(4);
    }

    [Fact]
    public async Task SeasonCalendarUnavailable_LeavesWeeksNull_AndStillAdvises()
    {
        var groupId = SeedLeague();
        SeedStandings(groupId);
        await DataContext.SaveChangesAsync();
        MockSlate(PickType.StraightUp, true, ThreeGames());
        _seasons
            .Setup(x => x.GetSeasonOverview(SeasonYear, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<SeasonOverviewDto>(default!, ResultStatus.Error, []));

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, null);

        result.IsSuccess.Should().BeTrue();
        result.Value.Analysis.RegularSeasonWeeksLeft.Should().BeNull();
        result.Value.Picks.Should().HaveCount(3);
    }

    [Fact]
    public async Task SlateFailure_PropagatesItsStatus()
    {
        var groupId = SeedLeague();
        await DataContext.SaveChangesAsync();
        _matchups
            .Setup(x => x.ExecuteAsync(It.IsAny<GetLeagueWeekMatchupsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<LeagueWeekMatchupsDto>(default!, ResultStatus.NotFound, []));

        var result = await CreateService().BuildAsync(_me, groupId, CurrentWeek, null);

        result.Status.Should().Be(ResultStatus.NotFound);
    }
}
