using AutoFixture;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Jobs;
using SportsData.Api.Application.Processors;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Season;
using SportsData.Core.Processing;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Jobs;

/// <summary>
/// Tests for the sport-agnostic MatchupScheduler. Validates iteration over
/// distinct sports in PickemGroups, per-sport SeasonClient resolution, and
/// the existing per-league week-creation + enqueue behavior.
/// </summary>
public class MatchupSchedulerTests : ApiTestBase<MatchupScheduler>
{
    private readonly Mock<IProvideSeasons> _footballSeasonClientMock = new();
    private readonly Mock<IProvideSeasons> _baseballSeasonClientMock = new();

    public MatchupSchedulerTests()
    {
        Mocker.GetMock<ISeasonClientFactory>()
            .Setup(x => x.Resolve(Sport.FootballNcaa))
            .Returns(_footballSeasonClientMock.Object);
        Mocker.GetMock<ISeasonClientFactory>()
            .Setup(x => x.Resolve(Sport.BaseballMlb))
            .Returns(_baseballSeasonClientMock.Object);
    }

    [Fact]
    public async Task ExecuteAsync_Iterates_Sports_With_Active_Leagues()
    {
        // Arrange — one league per sport.
        var footballLeagueId = Guid.NewGuid();
        var baseballLeagueId = Guid.NewGuid();
        var footballWeekId = Guid.NewGuid();
        var baseballWeekId = Guid.NewGuid();

        await SeedLeagueAsync(footballLeagueId, Sport.FootballNcaa, League.NCAAF);
        await SeedLeagueAsync(baseballLeagueId, Sport.BaseballMlb, League.MLB);

        SetupCurrentWeek(_footballSeasonClientMock, footballWeekId, 2026, 1, false, "regular");
        SetupCurrentWeek(_baseballSeasonClientMock, baseballWeekId, 2026, 5, false, "regular");

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var sut = Mocker.CreateInstance<MatchupScheduler>();

        // Act
        await sut.ExecuteAsync();

        // Assert — each sport's client was asked for its current week, and one
        // ScheduleGroupWeekMatchupsCommand was enqueued per league.
        _footballSeasonClientMock.Verify(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()), Times.Once);
        _baseballSeasonClientMock.Verify(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()), Times.Once);

        background.Verify(x => x.Enqueue<IScheduleGroupWeekMatchups>(
            It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_Skips_Sport_When_Current_Week_Unresolvable()
    {
        // Arrange — a baseball league but the baseball SeasonClient reports
        // no current week (offseason or transient failure).
        var leagueId = Guid.NewGuid();
        await SeedLeagueAsync(leagueId, Sport.BaseballMlb, League.MLB);

        _baseballSeasonClientMock
            .Setup(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<CanonicalSeasonWeekDto>(default!, ResultStatus.NotFound, []));

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var sut = Mocker.CreateInstance<MatchupScheduler>();

        // Act
        await sut.ExecuteAsync();

        // Assert — no week created, no command enqueued.
        background.Verify(x => x.Enqueue<IScheduleGroupWeekMatchups>(
            It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Skips_League_When_Week_Already_Generated()
    {
        // Arrange — a football league that already has a PickemGroupWeek for
        // the current SeasonWeek with AreMatchupsGenerated = true.
        var leagueId = Guid.NewGuid();
        var weekId = Guid.NewGuid();

        var group = BuildPickemGroup(leagueId, Sport.FootballNcaa, League.NCAAF);
        group.Weeks.Add(new PickemGroupWeek
        {
            Id = Guid.NewGuid(),
            GroupId = leagueId,
            SeasonWeekId = weekId,
            SeasonYear = 2026,
            SeasonWeek = 1,
            AreMatchupsGenerated = true,
        });

        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();

        SetupCurrentWeek(_footballSeasonClientMock, weekId, 2026, 1, false, "regular");

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var sut = Mocker.CreateInstance<MatchupScheduler>();

        // Act
        await sut.ExecuteAsync();

        // Assert
        background.Verify(x => x.Enqueue<IScheduleGroupWeekMatchups>(
            It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_Does_Nothing_When_No_Active_Leagues()
    {
        // Arrange — empty database (no PickemGroups).
        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var sut = Mocker.CreateInstance<MatchupScheduler>();

        // Act
        await sut.ExecuteAsync();

        // Assert — no SeasonClient call, no enqueue.
        _footballSeasonClientMock.Verify(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()), Times.Never);
        _baseballSeasonClientMock.Verify(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()), Times.Never);
        background.Verify(x => x.Enqueue<IScheduleGroupWeekMatchups>(
            It.IsAny<Expression<Func<IScheduleGroupWeekMatchups, Task>>>()), Times.Never);
    }

    private async Task SeedLeagueAsync(Guid leagueId, Sport sport, League league)
    {
        var group = BuildPickemGroup(leagueId, sport, league);
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();
    }

    private PickemGroup BuildPickemGroup(Guid leagueId, Sport sport, League league)
    {
        return new PickemGroup
        {
            Id = leagueId,
            Name = $"Test {sport} League",
            Sport = sport,
            League = league,
            PickType = PickType.StraightUp,
            TiebreakerType = TiebreakerType.None,
            TiebreakerTiePolicy = TiebreakerTiePolicy.EarliestSubmission,
            CommissionerUserId = Guid.NewGuid(),
            CreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = Guid.Empty,
        };
    }

    private static void SetupCurrentWeek(
        Mock<IProvideSeasons> seasonClient,
        Guid weekId,
        int seasonYear,
        int weekNumber,
        bool isNonStandardWeek,
        string seasonPhase)
    {
        seasonClient
            .Setup(x => x.GetCurrentSeasonWeek(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<CanonicalSeasonWeekDto>(new CanonicalSeasonWeekDto
            {
                Id = weekId,
                SeasonId = Guid.NewGuid(),
                SeasonYear = seasonYear,
                WeekNumber = weekNumber,
                IsNonStandardWeek = isNonStandardWeek,
                SeasonPhase = seasonPhase,
            }));
    }
}
