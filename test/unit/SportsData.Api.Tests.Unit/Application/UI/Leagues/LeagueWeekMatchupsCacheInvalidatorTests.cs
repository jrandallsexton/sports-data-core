using Moq;

using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Api.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues;

public class LeagueWeekMatchupsCacheInvalidatorTests : ApiTestBase<LeagueWeekMatchupsCacheInvalidator>
{
    private static readonly DateTime Now = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task EvictForContest_EvictsEveryLeagueWeekTheContestSitsIn_AndNothingElse()
    {
        var contestId = Guid.NewGuid();
        var leagueA = Guid.NewGuid();
        var leagueB = Guid.NewGuid();

        DataContext.PickemGroupMatchups.AddRange(
            Matchup(leagueA, contestId, week: 3),
            Matchup(leagueB, contestId, week: 3),
            Matchup(leagueA, Guid.NewGuid(), week: 4));
        await DataContext.SaveChangesAsync();

        var cache = Mocker.GetMock<ILeagueWeekMatchupsCache>();
        var sut = Mocker.CreateInstance<LeagueWeekMatchupsCacheInvalidator>();

        await sut.EvictForContestAsync(contestId);

        cache.Verify(c => c.RemoveAsync(leagueA, 3), Times.Once);
        cache.Verify(c => c.RemoveAsync(leagueB, 3), Times.Once);
        cache.Verify(c => c.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Exactly(2));
    }

    [Fact]
    public async Task EvictForContest_IsANoOp_WhenNoLeagueCarriesTheContest()
    {
        var sut = Mocker.CreateInstance<LeagueWeekMatchupsCacheInvalidator>();

        await sut.EvictForContestAsync(Guid.NewGuid());

        Mocker.GetMock<ILeagueWeekMatchupsCache>()
            .Verify(c => c.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task EvictForContest_SwallowsCacheFailures()
    {
        var contestId = Guid.NewGuid();
        DataContext.PickemGroupMatchups.Add(Matchup(Guid.NewGuid(), contestId, week: 3));
        await DataContext.SaveChangesAsync();

        Mocker.GetMock<ILeagueWeekMatchupsCache>()
            .Setup(c => c.RemoveAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .ThrowsAsync(new InvalidOperationException("redis down"));

        var sut = Mocker.CreateInstance<LeagueWeekMatchupsCacheInvalidator>();

        // The change that triggered the eviction is already committed.
        var ex = await Record.ExceptionAsync(() => sut.EvictForContestAsync(contestId));
        Assert.Null(ex);
    }

    private static PickemGroupMatchup Matchup(Guid groupId, Guid contestId, int week) => new()
    {
        Id = Guid.NewGuid(),
        GroupId = groupId,
        ContestId = contestId,
        SeasonWeekId = Guid.NewGuid(),
        SeasonYear = 2026,
        SeasonWeek = week,
        StartDateUtc = Now.AddDays(5),
        CreatedUtc = Now,
        CreatedBy = Guid.Empty
    };
}
