using AutoFixture;
using SportsData.Api.Application.Common.Enums;

using Moq;

using SportsData.Api.Application;
using SportsData.Api.Application.Scoring;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Picks;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Scoring;

public class PickScoringProcessorTests : ApiTestBase<PickScoringProcessor>
{
    private readonly Mock<IProvideContests> _contestClientMock = new();

    // Fixed "now" for any test-seeded timestamps. Using IDateTimeProvider
    // (via AutoMocker) instead of DateTime.UtcNow keeps test-seeded entities
    // deterministic per CLAUDE.md guidance.
    private static readonly DateTime FixedUtcNow = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public PickScoringProcessorTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);

        Mocker.GetMock<IDateTimeProvider>()
            .Setup(x => x.UtcNow())
            .Returns(FixedUtcNow);
    }
    [Fact]
    public async Task Process_WithValidMatchupResult_ScoresEachPick()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var result = Fixture.Build<MatchupResult>()
            .With(x => x.WinnerFranchiseSeasonId, Guid.NewGuid())
            .With(x => x.ContestId, contestId)
            .With(x => x.SeasonWeekId, seasonWeekId)
            .Create();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(result));

        var matchup = Fixture.Build<PickemGroupMatchup>()
            .With(x => x.ContestId, contestId)
            .With(x => x.SeasonWeekId, seasonWeekId)
            .With(x => x.GroupId, groupId)
            .Create();

        var group = Fixture.Build<PickemGroup>()
            .With(x => x.Id, groupId)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.PickType, PickType.StraightUp)
            .With (x => x.Weeks, new List<PickemGroupWeek>
            {
                new()
                {
                    SeasonWeekId = seasonWeekId,
                    Matchups = new List<PickemGroupMatchup>
                    {
                        matchup
                    },
                    SeasonYear = 2025,
                    SeasonWeek = 2,
                    GroupId = groupId
                }
            })
            .Create();

        await DataContext.PickemGroups.AddAsync(group);

        var picks = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, groupId)
            .With(x => x.Group, group)
            .With(x => x.FranchiseSeasonId, result.WinnerFranchiseSeasonId) // make it correct
            .With(x => x.ScoredAt, (DateTime?)null) // unscored, so the short-circuit doesn't fire
            .CreateMany(3)
            .ToList();

        await DataContext.UserPicks.AddRangeAsync(picks);
        await DataContext.SaveChangesAsync();

        var command = new ScorePicksCommand(contestId);

        var sut = Mocker.CreateInstance<PickScoringProcessor>();

        // Act
        await sut.Process(command);

        // Assert
        var scoring = Mocker.GetMock<IPickScoringService>();

        foreach (var pick in picks)
        {
            scoring.Verify(s =>
                s.ScorePick(
                    It.Is<PickemGroup>(g => g.Id == groupId),
                    It.IsAny<double?>(),
                    It.Is<PickemGroupUserPick>(p => p.Id == pick.Id),
                    result),
                Times.Once);
        }

        // Also ensure the result was fetched
        _contestClientMock
            .Verify(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_PublishesUserPickScored_WithPickedIsHome_ResolvedFromFranchiseSeasonId()
    {
        // Regression (#502 bug): pickedIsHome must be resolved against the
        // result's Home/AwayFranchiseSeasonId — the pick stores a
        // FranchiseSeasonId. Previously it compared against a bridged Franchise
        // id → always null → generic fallback copy.
        var contestId = Guid.NewGuid();
        var seasonWeekId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        var homeFsId = Guid.NewGuid();
        var awayFsId = Guid.NewGuid();

        var result = Fixture.Build<MatchupResult>()
            .With(x => x.ContestId, contestId)
            .With(x => x.SeasonWeekId, seasonWeekId)
            .With(x => x.HomeFranchiseSeasonId, homeFsId)
            .With(x => x.AwayFranchiseSeasonId, awayFsId)
            .With(x => x.WinnerFranchiseSeasonId, homeFsId)
            .With(x => x.AwayAbbreviation, "NYY")
            .With(x => x.HomeAbbreviation, "BOS")
            .With(x => x.FinalizedUtc, FixedUtcNow)
            .Create();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(result));

        var matchup = Fixture.Build<PickemGroupMatchup>()
            .With(x => x.ContestId, contestId)
            .With(x => x.SeasonWeekId, seasonWeekId)
            .With(x => x.GroupId, groupId)
            .Create();

        var group = Fixture.Build<PickemGroup>()
            .With(x => x.Id, groupId)
            .With(x => x.Sport, Sport.BaseballMlb)
            .With(x => x.PickType, PickType.StraightUp)
            .With(x => x.Weeks, new List<PickemGroupWeek>
            {
                new()
                {
                    SeasonWeekId = seasonWeekId,
                    Matchups = new List<PickemGroupMatchup> { matchup },
                    SeasonYear = 2026,
                    SeasonWeek = 2,
                    GroupId = groupId
                }
            })
            .Create();

        await DataContext.PickemGroups.AddAsync(group);

        // Pick the HOME side: FranchiseId = the home FranchiseSeasonId.
        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, groupId)
            .With(x => x.Group, group)
            .With(x => x.FranchiseSeasonId, homeFsId)
            .With(x => x.ScoredAt, (DateTime?)null)
            .Create();

        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();

        var sut = Mocker.CreateInstance<PickScoringProcessor>();

        await sut.Process(new ScorePicksCommand(contestId));

        Mocker.GetMock<IEventBus>().Verify(b => b.Publish(
            It.Is<UserPickScored>(e =>
                e.PickedIsHome == true &&
                e.AwayAbbreviation == "NYY" &&
                e.HomeAbbreviation == "BOS"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Process_WhenResultNotFound_LogsAndReturns()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // Seed a matchup + group so sport resolution succeeds and the test
        // exercises the GetMatchupResult NotFound path (rather than short-circuiting
        // on the new sport-resolution guard).
        var matchup = Fixture.Build<PickemGroupMatchup>()
            .With(x => x.ContestId, contestId)
            .With(x => x.GroupId, groupId)
            .Create();

        var group = Fixture.Build<PickemGroup>()
            .With(x => x.Id, groupId)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Weeks, new List<PickemGroupWeek>())
            .Create();

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, groupId)
            .With(x => x.Group, group)
            .With(x => x.ScoredAt, (DateTime?)null)
            .Create();

        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<MatchupResult>(default!, ResultStatus.NotFound, []));

        var command = new ScorePicksCommand(contestId);

        var sut = Mocker.CreateInstance<PickScoringProcessor>();

        // Act
        await sut.Process(command);

        // Assert
        Mocker.GetMock<IPickScoringService>()
            .Verify(x => x.ScorePick(
                    It.IsAny<PickemGroup>(),
                    It.IsAny<double?>(),
                    It.IsAny<PickemGroupUserPick>(),
                    It.IsAny<MatchupResult>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenGroupNotFound_LogsAndSkips()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var matchupGroupId = Guid.NewGuid(); // group that owns the matchup (for sport resolution)
        var missingGroupId = Guid.NewGuid(); // group referenced by the pick — intentionally absent

        var result = Fixture.Build<MatchupResult>()
            .With(x => x.WinnerFranchiseSeasonId, Guid.NewGuid())
            .Create();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(result));

        // Seed a matchup + group so sport resolution succeeds. The pick references
        // a DIFFERENT (missing) group, so the per-pick group lookup later fails as
        // the original test intends.
        var matchup = Fixture.Build<PickemGroupMatchup>()
            .With(x => x.ContestId, contestId)
            .With(x => x.GroupId, matchupGroupId)
            .Create();

        var matchupGroup = Fixture.Build<PickemGroup>()
            .With(x => x.Id, matchupGroupId)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Weeks, new List<PickemGroupWeek>())
            .Create();

        await DataContext.PickemGroups.AddAsync(matchupGroup);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, missingGroupId)
            .With(x => x.Group, null as PickemGroup)
            .With(x => x.ScoredAt, (DateTime?)null)
            .Create();

        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();

        var command = new ScorePicksCommand(contestId);

        var sut = Mocker.CreateInstance<PickScoringProcessor>();

        // Act
        await sut.Process(command);

        // Assert
        Mocker.GetMock<IPickScoringService>()
            .Verify(x => x.ScorePick(
                    It.IsAny<PickemGroup>(),
                    It.IsAny<double?>(),
                    It.IsAny<PickemGroupUserPick>(),
                    It.IsAny<MatchupResult>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenAllPicksAlreadyScored_ShortCircuits()
    {
        // Arrange
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        // Seed picks for the contest, all with ScoredAt set (already scored).
        // The short-circuit should fire before any Producer round-trip happens.
        var group = Fixture.Build<PickemGroup>()
            .With(x => x.Id, groupId)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Weeks, new List<PickemGroupWeek>())
            .Create();

        await DataContext.PickemGroups.AddAsync(group);

        var picks = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, groupId)
            .With(x => x.Group, group)
            .With(x => x.ScoredAt, (DateTime?)FixedUtcNow)
            .CreateMany(2)
            .ToList();

        await DataContext.UserPicks.AddRangeAsync(picks);
        await DataContext.SaveChangesAsync();

        var command = new ScorePicksCommand(contestId);

        var sut = Mocker.CreateInstance<PickScoringProcessor>();

        // Act
        await sut.Process(command);

        // Assert — no Producer call, no scoring
        _contestClientMock
            .Verify(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()), Times.Never);

        Mocker.GetMock<IPickScoringService>()
            .Verify(x => x.ScorePick(
                    It.IsAny<PickemGroup>(),
                    It.IsAny<double?>(),
                    It.IsAny<PickemGroupUserPick>(),
                    It.IsAny<MatchupResult>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenMatchupResultNotFinalized_LogsAndReturns()
    {
        // Regression: 2026-06-16. The PickScoringJob cron pulled contests with
        // unscored picks regardless of FinalizedUtc and produced
        // MatchupResults whose WinnerFranchiseSeasonId silently mapped to
        // Guid.Empty pre-enrichment. After the SQL filter + DTO nullability
        // change, the processor must refuse the result whenever
        // FinalizedUtc is null.
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var matchup = Fixture.Build<PickemGroupMatchup>()
            .With(x => x.ContestId, contestId)
            .With(x => x.GroupId, groupId)
            .Create();

        var group = Fixture.Build<PickemGroup>()
            .With(x => x.Id, groupId)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Weeks, new List<PickemGroupWeek>())
            .Create();

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.PickemGroupMatchups.AddAsync(matchup);

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, groupId)
            .With(x => x.Group, group)
            .With(x => x.ScoredAt, (DateTime?)null)
            .Create();

        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();

        var result = Fixture.Build<MatchupResult>()
            .With(x => x.ContestId, contestId)
            .With(x => x.FinalizedUtc, (DateTime?)null)
            .Create();

        _contestClientMock
            .Setup(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<MatchupResult>(result));

        var command = new ScorePicksCommand(contestId);
        var sut = Mocker.CreateInstance<PickScoringProcessor>();

        await sut.Process(command);

        Mocker.GetMock<IPickScoringService>()
            .Verify(x => x.ScorePick(
                    It.IsAny<PickemGroup>(),
                    It.IsAny<double?>(),
                    It.IsAny<PickemGroupUserPick>(),
                    It.IsAny<MatchupResult>()),
            Times.Never);
    }

    [Fact]
    public async Task Process_WhenSportCannotBeResolved_LogsAndReturns()
    {
        // Arrange — unscored picks but NO matchup row, so sport resolution returns null.
        var contestId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        var pick = Fixture.Build<PickemGroupUserPick>()
            .With(x => x.ContestId, contestId)
            .With(x => x.PickemGroupId, groupId)
            .With(x => x.Group, null as PickemGroup)
            .With(x => x.ScoredAt, (DateTime?)null)
            .Create();

        await DataContext.UserPicks.AddAsync(pick);
        await DataContext.SaveChangesAsync();

        var command = new ScorePicksCommand(contestId);

        var sut = Mocker.CreateInstance<PickScoringProcessor>();

        // Act
        await sut.Process(command);

        // Assert — no Producer call, no scoring
        _contestClientMock
            .Verify(x => x.GetMatchupResult(contestId, It.IsAny<CancellationToken>()), Times.Never);

        Mocker.GetMock<IPickScoringService>()
            .Verify(x => x.ScorePick(
                    It.IsAny<PickemGroup>(),
                    It.IsAny<double?>(),
                    It.IsAny<PickemGroupUserPick>(),
                    It.IsAny<MatchupResult>()),
            Times.Never);
    }
}
