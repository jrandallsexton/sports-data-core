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
    public void NeverMarked_IsNotSeen()
    {
        _cache.IsSeen("abc123").Should().BeFalse();
    }

    [Fact]
    public void Marked_IsSeen_WithinTtl()
    {
        _cache.MarkSeen("abc123");

        // 5h later — the longest possible stream is still inside the 8h TTL.
        _clock.Setup(x => x.UtcNow()).Returns(T0.AddHours(5));

        _cache.IsSeen("abc123").Should().BeTrue();
        _cache.IsSeen("other").Should().BeFalse("marking one hash must not mark others");
    }

    [Fact]
    public void Marked_ExpiresAfterTtl()
    {
        _cache.MarkSeen("abc123");

        _clock.Setup(x => x.UtcNow()).Returns(T0.AddHours(9));

        _cache.IsSeen("abc123").Should().BeFalse("entries expire after the 8h TTL");
    }

    [Fact]
    public void ReMarking_RefreshesTtl()
    {
        _cache.MarkSeen("abc123");

        // Re-marked at +6h (e.g. the item slipped through on a rewarm cycle);
        // at +10h the original mark would be expired but the refresh is not.
        _clock.Setup(x => x.UtcNow()).Returns(T0.AddHours(6));
        _cache.MarkSeen("abc123");

        _clock.Setup(x => x.UtcNow()).Returns(T0.AddHours(10));
        _cache.IsSeen("abc123").Should().BeTrue();
    }
}
