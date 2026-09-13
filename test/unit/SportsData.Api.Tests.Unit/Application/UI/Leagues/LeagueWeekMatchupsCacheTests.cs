using FluentAssertions;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Api.Application.UI.Leagues.Dtos;
using SportsData.Api.Application.UI.Leagues.Queries.GetLeagueWeekMatchups;
using SportsData.Core.Common;
using SportsData.Core.Extensions;

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Xunit;

namespace SportsData.Api.Tests.Unit.Application.UI.Leagues;

/// <summary>
/// The league-week matchups payload carries live game state — Status, Period, Clock,
/// AwayScore, HomeScore. Serving a cached copy mid-game would freeze the scoreboard on
/// the surface users watch precisely because it is moving. These tests pin the two rules
/// that prevent that: while anything in the week is live nothing is written, and a
/// pre-kickoff payload never outlives the kickoff that invalidates it.
/// </summary>
public class LeagueWeekMatchupsCacheTests
{
    private static readonly Guid LeagueId = Guid.Parse("0b5f2f8a-1111-2222-3333-444455556666");
    private const int Week = 1;

    /// <summary>Fixed clock. Kickoffs below are expressed relative to it.</summary>
    private static readonly DateTime Now = new(2026, 9, 13, 19, 0, 0, DateTimeKind.Utc);

    /// <summary>Far enough out that the kickoff cap never masks the status rules.</summary>
    private static readonly DateTime WellAfterNow = Now.AddHours(3);

    private static LeagueWeekMatchupsDto DtoWithStatuses(params string?[] statuses)
    {
        var dto = new LeagueWeekMatchupsDto();

        foreach (var status in statuses)
        {
            dto.Matchups.Add(new LeagueWeekMatchupsDto.MatchupForPickDto
            {
                Status = status,
                StartDateUtc = WellAfterNow
            });
        }

        return dto;
    }

    private static LeagueWeekMatchupsDto DtoWith(params (string? Status, DateTime Kickoff)[] matchups)
    {
        var dto = new LeagueWeekMatchupsDto();

        foreach (var (status, kickoff) in matchups)
        {
            dto.Matchups.Add(new LeagueWeekMatchupsDto.MatchupForPickDto
            {
                Status = status,
                StartDateUtc = kickoff
            });
        }

        return dto;
    }

    private static (LeagueWeekMatchupsCache Cache, Mock<IDistributedCache> Store) BuildSut()
    {
        var store = new Mock<IDistributedCache>();
        var clock = new Mock<IDateTimeProvider>();
        clock.Setup(x => x.UtcNow()).Returns(Now);

        return (
            new LeagueWeekMatchupsCache(store.Object, NullLogger<LeagueWeekMatchupsCache>.Instance, clock.Object),
            store);
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
    public async Task SetAsync_Caches_WhenWeekMixesPlayedAndUpcoming()
    {
        var (cache, store) = BuildSut();

        // The ordinary mid-week slate, modelled honestly: a game that has been
        // PLAYED (final, kickoff in the past) alongside one still to come. This is
        // the case the cache exists for — the long tail of pre- and post-game
        // browsing — and it is what the `!IsFinal` guard inside KickoffHasPassed
        // protects: without it a played game's past kickoff would read as "the
        // scoreboard is moving" and the whole week would go uncacheable.
        var dto = DtoWith(
            ("STATUS_FINAL", Now.AddHours(-4)),
            ("STATUS_SCHEDULED", Now.AddHours(2)));

        await cache.SetAsync(LeagueId, Week, dto);

        VerifyWritten(store, Times.Once());

        // The lifetime matters as much as the write. The kickoff cap must consider
        // only the UPCOMING kickoff — 2h out, so the 5-minute pregame TTL wins. Let
        // the played game's kickoff into that Min and the span becomes -4h, which
        // ResolveTtl passes straight through (it rejects null, not negatives) and
        // Redis is handed a negative expiry: an entry born expired, or a throw the
        // write path swallows. Either way the mid-week slate silently stops caching
        // while a write-happened assertion still goes green.
        CapturedFrom(store)!.AbsoluteExpirationRelativeToNow
            .Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task GetAsync_ReturnsAnEntry_ForAWeekMixingPlayedAndUpcoming()
    {
        var (cache, store) = BuildSut();

        // Read side of the same rule: a played game must not make the entry look
        // stale on every subsequent read (and get evicted for its trouble).
        var mixed = DtoWith(
            ("STATUS_FINAL", Now.AddHours(-4)),
            ("STATUS_SCHEDULED", Now.AddHours(2)));

        store.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(mixed.ToJson()));

        var result = await cache.GetAsync(LeagueId, Week);

        result.Should().NotBeNull("a finished game's past kickoff is settled, not moving");
        store.Verify(
            x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    // ─── Kickoff is part of the policy, not just status ──────────────────────────
    // The bug these pin: a slate cached at 3:29 with everything STATUS_SCHEDULED
    // stayed servable until 3:34 across a 3:30 kickoff, so cold loads rendered an
    // in-progress game as scheduled with no score until SignalR next spoke.

    [Fact]
    public async Task SetAsync_DoesNotCache_WhenAScheduledContestIsAlreadyPastKickoff()
    {
        var (cache, store) = BuildSut();

        // Kicked off a minute ago; the status row simply has not caught up yet.
        var dto = DtoWith(
            ("STATUS_FINAL", Now.AddHours(-4)),
            ("STATUS_SCHEDULED", Now.AddMinutes(-1)));

        await cache.SetAsync(LeagueId, Week, dto);

        VerifyWritten(store, Times.Never());
    }

    [Fact]
    public async Task SetAsync_CapsTheLifetimeAtTheNextKickoff()
    {
        var (cache, store) = BuildSut();

        // Next kickoff is 90 seconds out — well inside the 5-minute pregame TTL.
        var dto = DtoWith(
            ("STATUS_SCHEDULED", Now.AddSeconds(90)),
            ("STATUS_SCHEDULED", Now.AddHours(2)));

        await cache.SetAsync(LeagueId, Week, dto);

        VerifyWritten(store, Times.Once());
        CapturedFrom(store)!.AbsoluteExpirationRelativeToNow
            .Should().Be(TimeSpan.FromSeconds(90),
                "the entry must not survive the kickoff that invalidates it");
    }

    [Fact]
    public async Task SetAsync_KeepsThePregameLifetime_WhenKickoffIsFarOut()
    {
        var (cache, store) = BuildSut();

        var dto = DtoWith(("STATUS_SCHEDULED", Now.AddHours(6)));

        await cache.SetAsync(LeagueId, Week, dto);

        CapturedFrom(store)!.AbsoluteExpirationRelativeToNow
            .Should().Be(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task SetAsync_DoesNotCapTheSettledLifetime_WhenEverythingIsFinal()
    {
        var (cache, store) = BuildSut();

        // Kickoffs are all in the past, but nothing can change again.
        var dto = DtoWith(
            ("STATUS_FINAL", Now.AddHours(-5)),
            ("STATUS_FINAL_OT", Now.AddHours(-2)));

        await cache.SetAsync(LeagueId, Week, dto);

        CapturedFrom(store)!.AbsoluteExpirationRelativeToNow
            .Should().Be(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public async Task GetAsync_DiscardsAnEntry_WhoseContestHasKickedOffSinceItWasWritten()
    {
        var (cache, store) = BuildSut();

        // Written while scheduled; by the time it is read the kickoff has passed.
        var stale = DtoWith(("STATUS_SCHEDULED", Now.AddMinutes(-2)));

        store.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(stale.ToJson()));

        var result = await cache.GetAsync(LeagueId, Week);

        result.Should().BeNull("a kicked-off contest must fall through to the live read");

        // Evicted, not merely ignored: otherwise every request until natural
        // expiry re-reads and re-deserializes a payload already judged unusable.
        store.Verify(
            x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once());
    }

    [Fact]
    public async Task GetAsync_ReturnsAnEntry_WhileEveryContestIsStillAhead()
    {
        var (cache, store) = BuildSut();

        var fresh = DtoWith(("STATUS_SCHEDULED", Now.AddMinutes(20)));

        store.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Encoding.UTF8.GetBytes(fresh.ToJson()));

        var result = await cache.GetAsync(LeagueId, Week);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task SetAsync_UsesALongerLifetime_OnceEveryContestIsFinal()
    {
        var (cache, store) = BuildSut();

        await cache.SetAsync(LeagueId, Week, DtoWithStatuses("STATUS_FINAL"));
        var finalOptions = CapturedFrom(store);

        await cache.SetAsync(LeagueId, Week, DtoWithStatuses("STATUS_SCHEDULED"));
        var pregameOptions = CapturedFrom(store);

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

    /// <summary>Options from the most recent SetAsync the store received.</summary>
    private static DistributedCacheEntryOptions? CapturedFrom(Mock<IDistributedCache> store)
    {
        DistributedCacheEntryOptions? last = null;

        foreach (var invocation in store.Invocations)
        {
            if (invocation.Method.Name == nameof(IDistributedCache.SetAsync) &&
                invocation.Arguments.Count > 2 &&
                invocation.Arguments[2] is DistributedCacheEntryOptions options)
            {
                last = options;
            }
        }

        return last;
    }
}
