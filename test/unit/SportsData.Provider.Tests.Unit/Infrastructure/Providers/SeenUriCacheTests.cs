#nullable enable

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Infrastructure.Providers;

public class SeenUriCacheTests
{
    private static readonly DateTime T0 = new(2026, 9, 7, 18, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly SeenUriCache _cache;

    public SeenUriCacheTests()
    {
        _clock.Setup(x => x.UtcNow()).Returns(T0);
        _cache = new SeenUriCache(_clock.Object);
    }

    [Fact]
    public void FirstClaim_Succeeds()
    {
        _cache.TryMarkSeen("abc123").Should().BeTrue();
    }

    [Fact]
    public void SecondClaim_WithinTtl_Fails()
    {
        _cache.TryMarkSeen("abc123").Should().BeTrue();

        // 9 minutes later — the 10-minute claim is still live.
        _clock.Setup(x => x.UtcNow()).Returns(T0.AddMinutes(9));

        _cache.TryMarkSeen("abc123").Should().BeFalse("an unexpired claim suppresses re-enqueue");
        _cache.TryMarkSeen("other").Should().BeTrue("claiming one hash must not claim others");
    }

    [Fact]
    public void Claim_ReleasesAfterTtl()
    {
        _cache.TryMarkSeen("abc123").Should().BeTrue();

        // Past the 10-minute TTL: a job that never persisted must be allowed
        // to re-enqueue (the self-healing path for cleanly-failed jobs).
        _clock.Setup(x => x.UtcNow()).Returns(T0.AddMinutes(11));

        _cache.TryMarkSeen("abc123").Should().BeTrue("a lapsed claim is claimable again");
    }

    [Fact]
    public void ReClaim_AfterExpiry_RestartsTtl()
    {
        _cache.TryMarkSeen("abc123").Should().BeTrue();

        _clock.Setup(x => x.UtcNow()).Returns(T0.AddMinutes(11));
        _cache.TryMarkSeen("abc123").Should().BeTrue();

        // 9 minutes into the SECOND claim: still held.
        _clock.Setup(x => x.UtcNow()).Returns(T0.AddMinutes(20));
        _cache.TryMarkSeen("abc123").Should().BeFalse();
    }
}
