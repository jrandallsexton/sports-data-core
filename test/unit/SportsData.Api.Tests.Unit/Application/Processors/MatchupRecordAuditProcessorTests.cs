using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Processors;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Core.Infrastructure.Clients.Contest;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Processors;

/// <summary>
/// The league card reads the PickemGroupMatchup snapshot and never derives
/// (#769), so a wrong snapshot stays wrong until something rewrites it. These
/// pin the rules that make this audit safe to point at production: it corrects
/// only rows that actually differ, it evicts every league-week it examined so a
/// dropped eviction is recoverable by re-running, and a failed Producer call
/// stops loudly rather than reading as "nothing to fix".
/// </summary>
public class MatchupRecordAuditProcessorTests : ApiTestBase<MatchupRecordAuditProcessor>
{
    private readonly Mock<IProvideContests> _contestClientMock = new();
    private static readonly DateTime Now = new(2026, 9, 18, 12, 0, 0, DateTimeKind.Utc);

    public MatchupRecordAuditProcessorTests()
    {
        Mocker.GetMock<IContestClientFactory>()
            .Setup(x => x.Resolve(It.IsAny<Sport>()))
            .Returns(_contestClientMock.Object);

        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
    }

    [Fact]
    public async Task Process_CorrectsAWrongSnapshot_StampsModifiedUtc_AndEvictsThatLeagueWeek()
    {
        var groupId = await SeedGroupAsync();
        var contestId = Guid.NewGuid();
        // The shape the upsert defect produced: week 1 holding a later record.
        await SeedMatchupAsync(groupId, contestId, week: 1, awayWins: 3, awayLosses: 0, homeWins: 0, homeLosses: 3);

        SetupDerived(new EnteringRecordDto
        {
            ContestId = contestId,
            AwayWins = 0, AwayLosses = 0,
            HomeWins = 0, HomeLosses = 0
        });

        var result = await Mocker.CreateInstance<MatchupRecordAuditProcessor>()
            .Process(new MatchupRecordAuditCommand(Sport.FootballNcaa, 2026));

        result.Examined.Should().Be(1);
        result.Corrected.Should().Be(1);

        var saved = await DataContext.PickemGroupMatchups.FirstAsync(m => m.ContestId == contestId);
        saved.AwayWins.Should().Be(0);
        saved.HomeLosses.Should().Be(0);
        saved.ModifiedUtc.Should().Be(Now);

        Mocker.GetMock<ILeagueWeekMatchupsCache>()
            .Verify(c => c.RemoveAsync(groupId, 1), Times.Once);
    }

    [Fact]
    public async Task Process_WhenSnapshotAlreadyMatches_WritesNothingButStillEvicts()
    {
        var groupId = await SeedGroupAsync();
        var contestId = Guid.NewGuid();
        await SeedMatchupAsync(groupId, contestId, week: 3, awayWins: 2, awayLosses: 0, homeWins: 1, homeLosses: 1);

        SetupDerived(new EnteringRecordDto
        {
            ContestId = contestId,
            AwayWins = 2, AwayLosses = 0,
            HomeWins = 1, HomeLosses = 1
        });

        var result = await Mocker.CreateInstance<MatchupRecordAuditProcessor>()
            .Process(new MatchupRecordAuditCommand(Sport.FootballNcaa, 2026));

        result.Corrected.Should().Be(0);

        // ModifiedUtc untouched is the point: a clean re-run must not look like
        // a change, or the column stops meaning anything.
        var saved = await DataContext.PickemGroupMatchups.FirstAsync(m => m.ContestId == contestId);
        saved.ModifiedUtc.Should().BeNull();

        // Evicted anyway. RemoveAsync swallows store failures, so evicting only
        // corrected weeks would make a dropped eviction unrecoverable: the
        // rerun sees clean rows and would skip the week that still has a stale
        // payload cached.
        Mocker.GetMock<ILeagueWeekMatchupsCache>()
            .Verify(c => c.RemoveAsync(groupId, 3), Times.Once);
    }

    [Fact]
    public async Task Process_WhenProducerCallFails_AbortsWithoutWriting()
    {
        var groupId = await SeedGroupAsync();
        var contestId = Guid.NewGuid();
        await SeedMatchupAsync(groupId, contestId, week: 1, awayWins: 9, awayLosses: 9, homeWins: 9, homeLosses: 9);

        _contestClientMock
            .Setup(x => x.GetEnteringRecordsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Failure<List<EnteringRecordDto>>(default!, ResultStatus.Error, []));

        var act = async () => await Mocker.CreateInstance<MatchupRecordAuditProcessor>()
            .Process(new MatchupRecordAuditCommand(Sport.FootballNcaa, 2026));

        // Throws rather than returning a normal result: a 200 carrying a
        // correction count would claim work that SaveChangesAsync never ran on.
        await act.Should().ThrowAsync<InvalidOperationException>();

        var saved = await DataContext.PickemGroupMatchups.FirstAsync(m => m.ContestId == contestId);
        saved.AwayWins.Should().Be(9, "a failed page must not read as 'no corrections needed'");
        saved.ModifiedUtc.Should().BeNull();
    }

    [Fact]
    public async Task Process_ScopesToTheRequestedWeekAndSport()
    {
        var ncaa = await SeedGroupAsync(Sport.FootballNcaa);
        var nfl = await SeedGroupAsync(Sport.FootballNfl);

        var inScope = Guid.NewGuid();
        var otherWeek = Guid.NewGuid();
        var otherSport = Guid.NewGuid();
        await SeedMatchupAsync(ncaa, inScope, week: 2, awayWins: 5, awayLosses: 5, homeWins: 5, homeLosses: 5);
        await SeedMatchupAsync(ncaa, otherWeek, week: 3, awayWins: 5, awayLosses: 5, homeWins: 5, homeLosses: 5);
        await SeedMatchupAsync(nfl, otherSport, week: 2, awayWins: 5, awayLosses: 5, homeWins: 5, homeLosses: 5);

        List<Guid>? requested = null;
        _contestClientMock
            .Setup(x => x.GetEnteringRecordsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<List<Guid>, CancellationToken>((ids, _) => requested = ids)
            .ReturnsAsync(new Success<List<EnteringRecordDto>>(
            [
                new EnteringRecordDto { ContestId = inScope, AwayWins = 1, HomeWins = 1 }
            ]));

        var result = await Mocker.CreateInstance<MatchupRecordAuditProcessor>()
            .Process(new MatchupRecordAuditCommand(Sport.FootballNcaa, 2026, SeasonWeek: 2));

        result.Examined.Should().Be(1);
        requested.Should().BeEquivalentTo([inScope]);

        // Sport lives on the group, and each sport is a separate Producer
        // instance — mixing them in one batch would query the wrong database.
        (await DataContext.PickemGroupMatchups.FirstAsync(m => m.ContestId == otherSport))
            .AwayWins.Should().Be(5);
        (await DataContext.PickemGroupMatchups.FirstAsync(m => m.ContestId == otherWeek))
            .AwayWins.Should().Be(5);
    }

    [Fact]
    public async Task Process_CountsContestsProducerCouldNotResolve()
    {
        var groupId = await SeedGroupAsync();
        await SeedMatchupAsync(groupId, Guid.NewGuid(), week: 1, awayWins: 0, awayLosses: 0, homeWins: 0, homeLosses: 0);

        _contestClientMock
            .Setup(x => x.GetEnteringRecordsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<EnteringRecordDto>>([]));

        var result = await Mocker.CreateInstance<MatchupRecordAuditProcessor>()
            .Process(new MatchupRecordAuditCommand(Sport.FootballNcaa, 2026));

        result.Unresolved.Should().Be(1);
        result.Corrected.Should().Be(0);
    }

    private void SetupDerived(params EnteringRecordDto[] derived) =>
        _contestClientMock
            .Setup(x => x.GetEnteringRecordsByContestIds(It.IsAny<List<Guid>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Success<List<EnteringRecordDto>>(derived.ToList()));

    private async Task<Guid> SeedGroupAsync(Sport sport = Sport.FootballNcaa)
    {
        var id = Guid.NewGuid();
        DataContext.PickemGroups.Add(new PickemGroup
        {
            Id = id,
            Name = $"League {sport}",
            Sport = sport,
            League = sport == Sport.FootballNcaa ? League.NCAAF : League.NFL,
            CommissionerUserId = Guid.NewGuid(),
            SeasonYear = 2026,
            CreatedUtc = Now.AddDays(-30),
            CreatedBy = Guid.Empty
        });
        await DataContext.SaveChangesAsync();
        return id;
    }

    private async Task SeedMatchupAsync(
        Guid groupId, Guid contestId, int week,
        int awayWins, int awayLosses, int homeWins, int homeLosses)
    {
        DataContext.PickemGroupMatchups.Add(new PickemGroupMatchup
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            ContestId = contestId,
            SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2026,
            SeasonWeek = week,
            StartDateUtc = Now.AddDays(-7),
            AwayWins = awayWins,
            AwayLosses = awayLosses,
            HomeWins = homeWins,
            HomeLosses = homeLosses,
            CreatedUtc = Now.AddDays(-20),
            CreatedBy = Guid.Empty
        });
        await DataContext.SaveChangesAsync();
    }
}
