using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Api.Application.Common.Enums;
using SportsData.Api.Application.Matchups.Jobs.ApplyMatchupOdds;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data.Entities;
using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Contests;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.Matchups.Jobs.ApplyMatchupOdds;

public class MatchupOddsProcessorTests : ApiTestBase<MatchupOddsProcessor>
{
    private static readonly DateTime Kickoff = new(2026, 10, 3, 19, 30, 0, DateTimeKind.Utc);
    private static readonly DateTime Now = new(2026, 9, 30, 12, 0, 0, DateTimeKind.Utc);

    public MatchupOddsProcessorTests()
    {
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now);
    }

    private readonly Guid _contestId = Guid.NewGuid();

    private static readonly DisplayedContestOdds Odds = new()
    {
        ProviderId = "58",
        Details = "HOME -3.5",
        Spread = -3.5m,
        OverUnder = 52.5m,
        OverOdds = -115m,
        UnderOdds = -105m,
        AwayMoneyLine = 150,
        HomeMoneyLine = -175,
        AwaySpreadPrice = -112m,
        HomeSpreadPrice = -108m
    };

    private async Task<Guid> SeedGroupAsync(Sport sport, League league)
    {
        var group = new PickemGroup
        {
            Id = Guid.NewGuid(), Name = $"{sport} league", Sport = sport, League = league, CommissionerUserId = Guid.NewGuid()
        };
        await DataContext.PickemGroups.AddAsync(group);
        await DataContext.SaveChangesAsync();
        return group.Id;
    }

    private async Task SeedMatchupAsync(Guid groupId, Guid contestId, Action<PickemGroupMatchup>? configure = null)
    {
        var m = new PickemGroupMatchup
        {
            Id = Guid.NewGuid(), GroupId = groupId, ContestId = contestId, SeasonWeekId = Guid.NewGuid(),
            SeasonYear = 2026, SeasonWeek = 6, StartDateUtc = Kickoff
        };
        configure?.Invoke(m);
        await DataContext.PickemGroupMatchups.AddAsync(m);
        await DataContext.SaveChangesAsync();
    }

    // Odds versions (the Producer's event CreatedUtc).
    private static readonly DateTime V1 = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime V2 = V1.AddMinutes(5);

    private Task RunAsync(DisplayedContestOdds odds, Sport sport = Sport.FootballNcaa, DateTime? asOfUtc = null) =>
        Mocker.CreateInstance<MatchupOddsProcessor>()
            .Process(new ApplyMatchupOddsCommand(_contestId, sport, odds, Guid.NewGuid(), asOfUtc ?? V1));

    [Fact]
    public async Task WritesTheWholeDisplayedLine_ToEveryMatchupOfTheContest_InThatSport()
    {
        var leagueA = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        var leagueB = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        var otherContest = Guid.NewGuid();
        await SeedMatchupAsync(leagueA, _contestId);
        await SeedMatchupAsync(leagueB, _contestId);
        await SeedMatchupAsync(leagueA, otherContest);

        await RunAsync(Odds);

        var matchups = await DataContext.PickemGroupMatchups.ToListAsync();
        var applied = matchups.Where(m => m.ContestId == _contestId).ToList();
        Assert.Equal(2, applied.Count);
        foreach (var m in applied)
        {
            Assert.Equal("HOME -3.5", m.Spread);
            Assert.Equal(-3.5, m.HomeSpread);
            Assert.Equal(3.5, m.AwaySpread); // away = -home, as in the matchup SQL
            Assert.Equal(52.5, m.OverUnder);
            Assert.Equal(-115d, m.OverOdds);
            Assert.Equal(-105d, m.UnderOdds);
            Assert.Equal(150, m.AwayMoneyLine);
            Assert.Equal(-175, m.HomeMoneyLine);
            Assert.Equal(-112d, m.AwaySpreadPrice);
            Assert.Equal(-108d, m.HomeSpreadPrice);
        }
        Assert.Null(matchups.Single(m => m.ContestId == otherContest).HomeSpread);
    }

    [Fact]
    public async Task OnlyTouchesLeaguesOfTheEventsSport()
    {
        var nfl = await SeedGroupAsync(Sport.FootballNfl, League.NFL);
        await SeedMatchupAsync(nfl, _contestId);

        await RunAsync(Odds, Sport.FootballNcaa);

        Assert.Null(Assert.Single(await DataContext.PickemGroupMatchups.ToListAsync()).HomeMoneyLine);
    }

    [Fact]
    public async Task NeverErases_AFieldTheSnapshotLacks()
    {
        var league = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        await SeedMatchupAsync(league, _contestId, m =>
        {
            m.Spread = "HOME -3";
            m.HomeSpread = -3;
            m.AwaySpread = 3;
            m.OverOdds = -110;
            m.HomeMoneyLine = -160;
        });

        // The line moved but this snapshot has no spread text/number or over price.
        await RunAsync(new DisplayedContestOdds { ProviderId = "58", HomeMoneyLine = -175 });

        var m = Assert.Single(await DataContext.PickemGroupMatchups.ToListAsync());
        Assert.Equal("HOME -3", m.Spread);
        Assert.Equal(-3, m.HomeSpread);
        Assert.Equal(3, m.AwaySpread);
        Assert.Equal(-110d, m.OverOdds);
        Assert.Equal(-175, m.HomeMoneyLine);
    }

    [Fact]
    public async Task StampsModifiedUtc_OnlyWhenSomethingChanged()
    {
        var league = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        await SeedMatchupAsync(league, _contestId);

        await RunAsync(Odds);
        var m = Assert.Single(await DataContext.PickemGroupMatchups.AsNoTracking().ToListAsync());
        Assert.Equal(Now, m.ModifiedUtc);

        // A NEWER event with the same odds: the version advances (so an older
        // straggler is rejected later) but no value changed, so ModifiedUtc
        // must not move.
        Mocker.GetMock<IDateTimeProvider>().Setup(x => x.UtcNow()).Returns(Now.AddHours(1));
        await RunAsync(Odds, asOfUtc: V2);
        m = Assert.Single(await DataContext.PickemGroupMatchups.AsNoTracking().ToListAsync());
        Assert.Equal(Now, m.ModifiedUtc);
        Assert.Equal(V2, m.OddsAsOfUtc);
    }

    [Fact]
    public async Task EvictsTheLeagueWeekCache_ForWeeksThatChanged_AndNotForANoOp()
    {
        // The slate is served from ILeagueWeekMatchupsCache; without eviction
        // members keep reading the old line until the entry expires.
        var league = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        await SeedMatchupAsync(league, _contestId);
        var cache = Mocker.GetMock<ILeagueWeekMatchupsCache>();

        await RunAsync(Odds);
        cache.Verify(x => x.RemoveAsync(league, 6), Times.Once);

        // A newer event with the same odds: nothing changed, nothing to evict.
        await RunAsync(Odds, asOfUtc: V2);
        cache.Verify(x => x.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task AnOlderSnapshot_ArrivingLate_NeverOverwritesANewerOne()
    {
        // The delayed-retry case: V2 was applied, then V1's retried job runs.
        var league = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        await SeedMatchupAsync(league, _contestId);

        await RunAsync(Odds with { HomeMoneyLine = -200 }, asOfUtc: V2);
        await RunAsync(Odds with { HomeMoneyLine = -175 }, asOfUtc: V1);

        var m = Assert.Single(await DataContext.PickemGroupMatchups.AsNoTracking().ToListAsync());
        Assert.Equal(-200, m.HomeMoneyLine);
        Assert.Equal(V2, m.OddsAsOfUtc);
    }

    [Fact]
    public async Task ARetryOfTheSameJob_IsSkipped()
    {
        var league = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        await SeedMatchupAsync(league, _contestId);
        var cache = Mocker.GetMock<ILeagueWeekMatchupsCache>();

        await RunAsync(Odds, asOfUtc: V1);
        await RunAsync(Odds with { HomeMoneyLine = -999 }, asOfUtc: V1);

        var m = Assert.Single(await DataContext.PickemGroupMatchups.AsNoTracking().ToListAsync());
        Assert.Equal(-175, m.HomeMoneyLine);
        cache.Verify(x => x.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task ANewerSnapshot_Applies_AndRecordsItsVersion()
    {
        var league = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        await SeedMatchupAsync(league, _contestId);

        await RunAsync(Odds, asOfUtc: V1);
        await RunAsync(Odds with { HomeMoneyLine = -200 }, asOfUtc: V2);

        var m = Assert.Single(await DataContext.PickemGroupMatchups.AsNoTracking().ToListAsync());
        Assert.Equal(-200, m.HomeMoneyLine);
        Assert.Equal(V2, m.OddsAsOfUtc);
    }

    [Fact]
    public async Task AnUnspecifiedKindVersion_IsStoredAsUtc()
    {
        // Npgsql writes timestamptz only from Utc; a serializer round trip may drop the kind.
        var league = await SeedGroupAsync(Sport.FootballNcaa, League.NCAAF);
        await SeedMatchupAsync(league, _contestId);

        await RunAsync(Odds, asOfUtc: DateTime.SpecifyKind(V1, DateTimeKind.Unspecified));

        var m = Assert.Single(await DataContext.PickemGroupMatchups.AsNoTracking().ToListAsync());
        Assert.Equal(DateTimeKind.Utc, m.OddsAsOfUtc!.Value.Kind);
        Assert.Equal(V1, m.OddsAsOfUtc);
    }

    [Fact]
    public async Task NoLeagueCarriesTheContest_IsANoOp()
    {
        await RunAsync(Odds);

        Assert.Empty(await DataContext.PickemGroupMatchups.ToListAsync());
    }
}
