using FluentAssertions;

using FluentValidation.Results;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Producer.Application.Competitions.Queries;
using SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutDrives;
using SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutMetrics;
using SportsData.Producer.Application.Competitions.Queries.GetCompetitionsWithoutPlays;
using SportsData.Producer.Application.GroupSeasons.Queries.GetFbsGroupSeasonIds;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Metrics;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Competitions.Queries;

/// <summary>
/// The plays / drives / metrics coverage diagnostics, run through the real
/// <see cref="CompetitionDiagnosticsScope"/>: the pod's sport, one season
/// (default: the latest started), started games only, and FBS-involved
/// games only on the NCAA pod.
/// </summary>
public class CompetitionDiagnosticsQueryHandlerTests : ProducerTestBase<GetCompetitionsWithoutPlaysQueryHandler>
{
    private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid FbsGroupId = Guid.NewGuid();

    private void UseScope(Sport sport)
    {
        Mocker.GetMock<IAppMode>().Setup(x => x.CurrentSport).Returns(sport);
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
        Mocker.GetMock<IGetFbsGroupSeasonIdsQueryHandler>()
            .Setup(x => x.ExecuteAsync(It.IsAny<GetFbsGroupSeasonIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<HashSet<Guid>>([FbsGroupId]));
        Mocker.Use<ICompetitionDiagnosticsScope>(Mocker.CreateInstance<CompetitionDiagnosticsScope>());
    }

    private async Task<Guid> SeedFranchiseSeasonAsync(Guid? groupSeasonId)
    {
        var id = Guid.NewGuid();
        FootballDataContext.FranchiseSeasons.Add(new FranchiseSeason
        {
            Id = id,
            FranchiseId = Guid.NewGuid(),
            SeasonYear = 2026,
            GroupSeasonId = groupSeasonId,
            Slug = id.ToString("N"),
            Location = "Loc",
            Abbreviation = "ABC",
            DisplayName = "Team",
            DisplayNameShort = "Team",
            ColorCodeHex = "000000"
        });
        await FootballDataContext.SaveChangesAsync();
        return id;
    }

    private async Task<Guid> SeedCompetitionAsync(
        string name,
        Sport sport = Sport.FootballNfl,
        int seasonYear = 2026,
        DateTime? startDateUtc = null,
        int plays = 0,
        int playsWithDrive = 0,
        bool withMetric = false,
        Guid? homeFranchiseSeasonId = null,
        Guid? awayFranchiseSeasonId = null,
        bool cancelled = false)
    {
        var contestId = Guid.NewGuid();
        var competitionId = Guid.NewGuid();
        var start = startDateUtc ?? Now.AddDays(-7);

        FootballDataContext.Contests.Add(new FootballContest
        {
            Id = contestId,
            CreatedBy = Guid.Empty,
            CreatedUtc = start.AddDays(-7),
            Sport = sport,
            SeasonYear = seasonYear,
            Name = name,
            ShortName = name,
            StartDateUtc = start,
            HomeTeamFranchiseSeasonId = homeFranchiseSeasonId ?? Guid.Empty,
            AwayTeamFranchiseSeasonId = awayFranchiseSeasonId ?? Guid.Empty,
            CancelledUtc = cancelled ? start.AddHours(-1) : null
        });
        FootballDataContext.Competitions.Add(new FootballCompetition
        {
            Id = competitionId,
            ContestId = contestId,
            CreatedBy = Guid.Empty,
            CreatedUtc = start.AddDays(-7),
            Date = start
        });

        for (var i = 0; i < plays; i++)
        {
            FootballDataContext.CompetitionPlays.Add(new FootballCompetitionPlay
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                DriveId = i < playsWithDrive ? Guid.NewGuid() : null,
                EspnId = $"{competitionId:N}-{i}",
                SequenceNumber = i.ToString(),
                TypeId = "0",
                Text = $"play {i}"
            });
        }

        if (withMetric)
        {
            FootballDataContext.CompetitionMetrics.Add(new CompetitionMetric
            {
                Id = Guid.NewGuid(),
                CompetitionId = competitionId,
                FranchiseSeasonId = Guid.NewGuid(),
                Season = seasonYear
            });
        }

        await FootballDataContext.SaveChangesAsync();
        return competitionId;
    }

    [Fact]
    public async Task Plays_FlagsGamesWithTenOrFewerPlays_StartedOnly_StampedWithSport()
    {
        UseScope(Sport.FootballNfl);
        var sparse = await SeedCompetitionAsync("Sparse", plays: 10, startDateUtc: Now.AddDays(-2));
        var empty = await SeedCompetitionAsync("Empty", plays: 0, startDateUtc: Now.AddDays(-9));
        await SeedCompetitionAsync("Full", plays: 11);
        await SeedCompetitionAsync("Not started", plays: 0, startDateUtc: Now.AddDays(1));

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutPlaysQueryHandler>();

        var result = await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(null));

        result.IsSuccess.Should().BeTrue();
        result.Value.Select(x => x.CompetitionId).Should().Equal([empty, sparse], "ordered by start date");
        result.Value.Should().OnlyContain(x => x.Sport == Sport.FootballNfl);
        result.Value.Single(x => x.CompetitionId == sparse).PlayCount.Should().Be(10);
    }

    [Fact]
    public async Task Scope_DefaultsToTheLatestStartedSeason_AndHonoursAnExplicitOne()
    {
        UseScope(Sport.FootballNfl);
        var lastSeason = await SeedCompetitionAsync("Last season", seasonYear: 2025, startDateUtc: Now.AddDays(-300));
        var thisSeason = await SeedCompetitionAsync("This season", seasonYear: 2026);
        // A scheduled game in a later season must not move the default.
        await SeedCompetitionAsync("Next season", seasonYear: 2027, startDateUtc: Now.AddDays(200));

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutPlaysQueryHandler>();

        (await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(null)))
            .Value.Select(x => x.CompetitionId).Should().Equal(thisSeason);
        (await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(2025)))
            .Value.Select(x => x.CompetitionId).Should().Equal(lastSeason);
    }

    [Fact]
    public async Task Scope_OnTheNcaaPod_KeepsOnlyGamesWithAnFbsTeam()
    {
        UseScope(Sport.FootballNcaa);
        var fbs = await SeedFranchiseSeasonAsync(FbsGroupId);
        var fcs = await SeedFranchiseSeasonAsync(Guid.NewGuid());
        var fcs2 = await SeedFranchiseSeasonAsync(Guid.NewGuid());
        var fbsAway = await SeedCompetitionAsync("FBS team away", Sport.FootballNcaa,
            homeFranchiseSeasonId: fcs, awayFranchiseSeasonId: fbs);
        await SeedCompetitionAsync("FCS vs FCS", Sport.FootballNcaa,
            homeFranchiseSeasonId: fcs, awayFranchiseSeasonId: fcs2);

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutPlaysQueryHandler>();

        var result = await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(null));

        result.Value.Select(x => x.CompetitionId).Should().Equal(fbsAway);
        Mocker.GetMock<IGetFbsGroupSeasonIdsQueryHandler>().Verify(
            x => x.ExecuteAsync(It.Is<GetFbsGroupSeasonIdsQuery>(q => q.SeasonYear == 2026), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Scope_OnTheNcaaPod_PropagatesAMissingFbsHierarchy()
    {
        UseScope(Sport.FootballNcaa);
        Mocker.GetMock<IGetFbsGroupSeasonIdsQueryHandler>()
            .Setup(x => x.ExecuteAsync(It.IsAny<GetFbsGroupSeasonIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<HashSet<Guid>>(
                default!, ResultStatus.NotFound, [new ValidationFailure("SeasonYear", "FBS group root(s) not found")]));
        await SeedCompetitionAsync("Any", Sport.FootballNcaa);

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutPlaysQueryHandler>();

        var result = await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(null));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Scope_OnTheNflPod_NeverAsksForFbs()
    {
        UseScope(Sport.FootballNfl);
        await SeedCompetitionAsync("Bears at Packers");

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutPlaysQueryHandler>();

        (await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(null))).Value.Should().ContainSingle();
        Mocker.GetMock<IGetFbsGroupSeasonIdsQueryHandler>().Verify(
            x => x.ExecuteAsync(It.IsAny<GetFbsGroupSeasonIdsQuery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Drives_FlagsGamesWithPlaysButNoDriveLinks()
    {
        UseScope(Sport.FootballNfl);
        var orphaned = await SeedCompetitionAsync("Orphaned", plays: 30, playsWithDrive: 0);
        await SeedCompetitionAsync("Linked", plays: 30, playsWithDrive: 1);
        await SeedCompetitionAsync("No plays", plays: 0);

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutDrivesQueryHandler>();

        var result = await sut.ExecuteAsync(new GetCompetitionsWithoutDrivesQuery(null));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Should().ContainSingle().Subject;
        row.CompetitionId.Should().Be(orphaned);
        row.PlayCount.Should().Be(30);
        row.PlaysWithDriveId.Should().Be(0);
        row.Sport.Should().Be(Sport.FootballNfl);
    }

    [Fact]
    public async Task Metrics_FlagsGamesWithNoMetricRows()
    {
        UseScope(Sport.FootballNfl);
        var missing = await SeedCompetitionAsync("Missing");
        await SeedCompetitionAsync("Computed", withMetric: true);

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutMetricsQueryHandler>();

        var result = await sut.ExecuteAsync(new GetCompetitionsWithoutMetricsQuery(null));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Should().ContainSingle().Subject;
        row.CompetitionId.Should().Be(missing);
        row.MetricCount.Should().Be(0);
        row.Sport.Should().Be(Sport.FootballNfl);
    }

    [Fact]
    public async Task Scope_AnExplicitSeasonWithNoStartedContests_IsNotFound_NotAnEmptyClean()
    {
        UseScope(Sport.FootballNfl);
        await SeedCompetitionAsync("This season");

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutPlaysQueryHandler>();

        var result = await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(2052));

        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task Scope_ExcludesCancelledContests_FromResultsAndSeasonSelection()
    {
        UseScope(Sport.FootballNfl);
        var played = await SeedCompetitionAsync("Played", seasonYear: 2026);
        await SeedCompetitionAsync("Cancelled", seasonYear: 2026, cancelled: true);
        // A cancelled game alone must neither move the default season nor make an explicit season "started".
        await SeedCompetitionAsync("Cancelled next season", seasonYear: 2027, startDateUtc: Now.AddDays(-1), cancelled: true);

        var sut = Mocker.CreateInstance<GetCompetitionsWithoutPlaysQueryHandler>();

        (await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(null)))
            .Value.Select(x => x.CompetitionId).Should().Equal(played);
        (await sut.ExecuteAsync(new GetCompetitionsWithoutPlaysQuery(2027)))
            .Status.Should().Be(ResultStatus.NotFound);
    }
}
