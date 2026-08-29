using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Providers.Espn;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Infrastructure.Providers;

/// <summary>
/// Negative cache for ESPN 400s: marked URIs are suppressed for the TTL,
/// query-string variants collapse to one entry (paging/lang params vary
/// per request but ESPN's "unsupported" verdict is per resource), entries
/// expire so a resource that gains support is re-probed, and the backing
/// table makes the knowledge durable — a second cache instance (a fresh
/// pod) hydrates everything the first one learned.
/// </summary>
public class KnownBadUriCacheTests
{
    private static readonly DateTime FixedNow = new(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly IServiceScopeFactory _scopeFactory;

    public KnownBadUriCacheTests()
    {
        _clock.Setup(x => x.UtcNow()).Returns(FixedNow);

        // Real scope factory over a shared InMemory database so the cache's
        // scoped AppDataContext resolution works exactly as in production.
        var dbName = Guid.NewGuid().ToString()[..8];
        var services = new ServiceCollection();
        services.AddDbContext<AppDataContext>(o => o.UseInMemoryDatabase(dbName));
        _scopeFactory = services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private KnownBadUriCache CreateCache() => new(
        _scopeFactory,
        _clock.Object,
        NullLogger<KnownBadUriCache>.Instance);

    [Fact]
    public async Task UnmarkedUri_IsNotKnownBad()
    {
        var cache = CreateCache();

        (await cache.IsKnownBadAsync(new Uri("http://sports.core.api.espn.com/v2/anything")))
            .Should().BeFalse();
    }

    [Fact]
    public async Task MarkedUri_IsKnownBad()
    {
        var cache = CreateCache();
        var uri = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401866615/competitions/401866615/probabilities");

        await cache.MarkBadAsync(uri, KnownBadReason.BadRequest);

        (await cache.IsKnownBadAsync(uri)).Should().BeTrue();
    }

    [Fact]
    public async Task QueryStringVariants_CollapseToOneEntry()
    {
        var cache = CreateCache();

        await cache.MarkBadAsync(new Uri("http://sports.core.api.espn.com/v2/events/1/probabilities?lang=en&region=us"), KnownBadReason.BadRequest);

        (await cache.IsKnownBadAsync(new Uri("http://sports.core.api.espn.com/v2/events/1/probabilities?lang=en&region=us&page=2")))
            .Should().BeTrue();
        (await cache.IsKnownBadAsync(new Uri("http://sports.core.api.espn.com/v2/events/1/probabilities")))
            .Should().BeTrue();
        // A different path is a different resource.
        (await cache.IsKnownBadAsync(new Uri("http://sports.core.api.espn.com/v2/events/2/probabilities")))
            .Should().BeFalse();
    }

    [Fact]
    public async Task MarkedUri_ExpiresAfterTtl()
    {
        var cache = CreateCache();
        var uri = new Uri("http://sports.core.api.espn.com/v2/events/1/probabilities");
        await cache.MarkBadAsync(uri, KnownBadReason.BadRequest);

        _clock.Setup(x => x.UtcNow()).Returns(FixedNow.AddHours(13));

        (await cache.IsKnownBadAsync(uri)).Should().BeFalse();
    }

    [Fact]
    public async Task FreshInstance_HydratesFromDatabase()
    {
        // The new-KEDA-pod scenario: pod A learns a bad URI; pod B (a brand
        // new cache instance over the same database) must know it too.
        var uri = new Uri("http://sports.core.api.espn.com/v2/events/1/probabilities");
        await CreateCache().MarkBadAsync(uri, KnownBadReason.BadRequest);

        var freshPod = CreateCache();

        (await freshPod.IsKnownBadAsync(uri)).Should().BeTrue();
    }

    [Fact]
    public async Task ExpiredRows_AreNotHydrated_AndArePrunedOnWrite()
    {
        var expiredUri = new Uri("http://sports.core.api.espn.com/v2/events/1/probabilities");
        var freshUri = new Uri("http://sports.core.api.espn.com/v2/events/2/probabilities");
        await CreateCache().MarkBadAsync(expiredUri, KnownBadReason.BadRequest);

        // 13h later the first entry is expired; a new pod marks a second URI.
        _clock.Setup(x => x.UtcNow()).Returns(FixedNow.AddHours(13));
        var freshPod = CreateCache();
        await freshPod.MarkBadAsync(freshUri, KnownBadReason.BadRequest);

        (await freshPod.IsKnownBadAsync(expiredUri)).Should().BeFalse();
        (await freshPod.IsKnownBadAsync(freshUri)).Should().BeTrue();

        // The expired row SURVIVES expiry (FailureCount must persist for
        // the NotFound backoff) — it is pruned only past the 7-day grace.
        using var scope = _scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<AppDataContext>();
        (await dataContext.EspnKnownBadUris.CountAsync()).Should().Be(2);

        // 8 days later a new write prunes it.
        _clock.Setup(x => x.UtcNow()).Returns(FixedNow.AddDays(8));
        await freshPod.MarkBadAsync(freshUri, KnownBadReason.BadRequest);
        (await dataContext.EspnKnownBadUris.AsNoTracking().CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task NotFound_BacksOffExponentially_AndEscalationSurvivesNewPod()
    {
        var uri = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2026/athletes/5421843");
        var cache = CreateCache();

        // Failure 1: suppressed 5 minutes — expired at +6m.
        await cache.MarkBadAsync(uri, KnownBadReason.NotFound);
        (await cache.IsKnownBadAsync(uri)).Should().BeTrue();
        _clock.Setup(x => x.UtcNow()).Returns(FixedNow.AddMinutes(6));
        (await cache.IsKnownBadAsync(uri)).Should().BeFalse();

        // Failure 2 (from a FRESH pod — escalation is persisted): 10 minutes.
        var freshPod = CreateCache();
        await freshPod.MarkBadAsync(uri, KnownBadReason.NotFound);
        _clock.Setup(x => x.UtcNow()).Returns(FixedNow.AddMinutes(6 + 9));
        (await freshPod.IsKnownBadAsync(uri)).Should().BeTrue();
        _clock.Setup(x => x.UtcNow()).Returns(FixedNow.AddMinutes(6 + 11));
        (await freshPod.IsKnownBadAsync(uri)).Should().BeFalse();
    }

    [Fact]
    public async Task NotFound_BackoffCapsAtSixHours()
    {
        var uri = new Uri("http://sports.core.api.espn.com/v2/dead");
        var cache = CreateCache();

        // Drive the failure count far past the cap threshold.
        for (var i = 0; i < 12; i++)
        {
            await cache.MarkBadAsync(uri, KnownBadReason.NotFound);
        }

        // Still suppressed just before six hours...
        _clock.Setup(x => x.UtcNow()).Returns(FixedNow.AddMinutes(355));
        (await cache.IsKnownBadAsync(uri)).Should().BeTrue();
        // ...and expired just after.
        _clock.Setup(x => x.UtcNow()).Returns(FixedNow.AddMinutes(365));
        (await cache.IsKnownBadAsync(uri)).Should().BeFalse();
    }
}
