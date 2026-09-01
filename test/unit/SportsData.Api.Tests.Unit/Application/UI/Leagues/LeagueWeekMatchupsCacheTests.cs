using FluentAssertions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues;

/// <summary>
/// The league-week matchups payload carries live game state — Status, Period, Clock,
/// AwayScore, HomeScore. Serving a cached copy mid-game would freeze the scoreboard on
/// the surface users watch precisely because it is moving. These tests pin the rule that
/// prevents that: while anything in the week is live, nothing is written to the cache.
/// </summary>
public class LeagueWeekMatchupsCacheTests
{
    private static readonly Guid LeagueId = Guid.Parse("0b5f2f8a-1111-2222-3333-444455556666");
    private const int Week = 1;

    private static LeagueWeekMatchupsDto DtoWithStatuses(params string?[] statuses)
    {
        var dto = new LeagueWeekMatchupsDto();

        foreach (var status in statuses)
        {
            dto.Matchups.Add(new LeagueWeekMatchupsDto.MatchupForPickDto { Status = status });
        }

        return dto;
    }

    private static (LeagueWeekMatchupsCache Cache, Mock<IDistributedCache> Store) BuildSut()
    {
        var store = new Mock<IDistributedCache>();
        return (new LeagueWeekMatchupsCache(store.Object, NullLogger<LeagueWeekMatchupsCache>.Instance), store);
    }

    private static void VerifyWritten(Mock<IDistributedCache> store, Times times) =>
        store.Verify(
            x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()),
            times);

    [Theory]
    [InlineData("STATUS_IN_PROGRESS")]
    [InlineData("STATUS_HALFTIME")]
    [InlineData("STATUS_END_PERIOD")]
    [InlineData("STATUS_DELAYED")]
    public async Task SetAsync_DoesNotCache_WhenAnyContestIsLive(string liveStatus)
    {
        var (cache, store) = BuildSut();

        // One live game among finished ones is still a live week.
        var dto = DtoWithStatuses("STATUS_FINAL", liveStatus, "STATUS_FINAL");

        await cache.SetAsync(LeagueId, Week, dto);

        VerifyWritten(store, Times.Never());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("SOMETHING_ESPN_INVENTED_LATER")]
    public async Task SetAsync_DoesNotCache_WhenAStatusIsUnrecognised(string? unknownStatus)
    {
        var (cache, store) = BuildSut();

        var dto = DtoWithStatuses("STATUS_SCHEDULED", unknownStatus);

        await cache.SetAsync(LeagueId, Week, dto);

        // Fail closed: an unfamiliar status degrades to no caching rather than
        // silently pinning a stale scoreboard.
        VerifyWritten(store, Times.Never());
    }

    [Fact]
    public async Task SetAsync_Caches_WhenEveryContestIsScheduled()
    {
        var (cache, store) = BuildSut();

        var dto = DtoWithStatuses("STATUS_SCHEDULED", "STATUS_SCHEDULED");

        await cache.SetAsync(LeagueId, Week, dto);

        VerifyWritten(store, Times.Once());
    }

    [Fact]
    public async Task SetAsync_Caches_WhenEveryContestIsFinal()
    {
        var (cache, store) = BuildSut();

        var dto = DtoWithStatuses("STATUS_FINAL", "STATUS_FINAL_OT");

        await cache.SetAsync(LeagueId, Week, dto);

        VerifyWritten(store, Times.Once());
    }

    [Fact]
    public async Task SetAsync_Caches_WhenWeekMixesScheduledAndFinal()
    {
        var (cache, store) = BuildSut();

        // Mid-week: some games played, none in progress. Safe to serve again.
        var dto = DtoWithStatuses("STATUS_FINAL", "STATUS_SCHEDULED");

        await cache.SetAsync(LeagueId, Week, dto);

        VerifyWritten(store, Times.Once());
    }

    [Fact]
    public async Task SetAsync_UsesALongerLifetime_OnceEveryContestIsFinal()
    {
        var (cache, store) = BuildSut();

        DistributedCacheEntryOptions? finalOptions = null;
        DistributedCacheEntryOptions? pregameOptions = null;

        store.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, _, options, _) => finalOptions = options)
            .Returns(Task.CompletedTask);

        await cache.SetAsync(LeagueId, Week, DtoWithStatuses("STATUS_FINAL"));

        store.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (_, _, options, _) => pregameOptions = options)
            .Returns(Task.CompletedTask);

        await cache.SetAsync(LeagueId, Week, DtoWithStatuses("STATUS_SCHEDULED"));

        finalOptions!.AbsoluteExpirationRelativeToNow
            .Should().BeGreaterThan(pregameOptions!.AbsoluteExpirationRelativeToNow!.Value);
    }

    [Fact]
    public async Task SetAsync_Caches_WhenTheWeekHasNoMatchups()
    {
        var (cache, store) = BuildSut();

        // An empty week has no live state to go stale.
        await cache.SetAsync(LeagueId, Week, new LeagueWeekMatchupsDto());

        VerifyWritten(store, Times.Once());
    }

    [Fact]
    public async Task GetAsync_And_SetAsync_AgreeOnTheKey()
    {
        var (cache, store) = BuildSut();

        string? writtenKey = null;

        store.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (key, _, _, _) => writtenKey = key)
            .Returns(Task.CompletedTask);

        await cache.SetAsync(LeagueId, Week, DtoWithStatuses("STATUS_SCHEDULED"));
        await cache.GetAsync(LeagueId, Week);

        store.Verify(
            x => x.GetAsync(writtenKey!, It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task Key_IsScopedToLeagueAndWeek()
    {
        var (cache, store) = BuildSut();

        var keys = new List<string>();

        store.Setup(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<DistributedCacheEntryOptions>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, byte[], DistributedCacheEntryOptions, CancellationToken>(
                (key, _, _, _) => keys.Add(key))
            .Returns(Task.CompletedTask);

        var otherLeague = Guid.Parse("99999999-8888-7777-6666-555544443333");

        await cache.SetAsync(LeagueId, 1, DtoWithStatuses("STATUS_SCHEDULED"));
        await cache.SetAsync(LeagueId, 2, DtoWithStatuses("STATUS_SCHEDULED"));
        await cache.SetAsync(otherLeague, 1, DtoWithStatuses("STATUS_SCHEDULED"));

        keys.Should().OnlyHaveUniqueItems(
            "a league or week must never collide with another league or week");
    }
}
