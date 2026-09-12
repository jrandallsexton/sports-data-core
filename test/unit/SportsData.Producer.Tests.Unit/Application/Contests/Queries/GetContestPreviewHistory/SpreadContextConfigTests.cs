#nullable enable
using FluentAssertions;

using Microsoft.Extensions.Configuration;

using Moq;

using SportsData.Producer.Application.Contests.Queries.Matchups.GetContestPreviewHistory;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Contests.Queries.GetContestPreviewHistory;

public class SpreadContextConfigTests
{
    private const string LadderKey = "SportsData.Producer:SpreadContext:AtsKeyNumbers";
    private const string GuardKey = "SportsData.Producer:SpreadContext:AtsBucketMaxDistancePoints";

    private static IConfiguration ConfigWith(string? ladder, string? guard = null)
    {
        // FromConfiguration reads only the indexer, so a mock keeps this
        // free of Microsoft.Extensions.Configuration binder packages.
        var config = new Mock<IConfiguration>();
        config.Setup(x => x[LadderKey]).Returns(ladder);
        config.Setup(x => x[GuardKey]).Returns(guard);
        return config.Object;
    }

    [Fact]
    public void WhenNoConfigValues_UsesCodeDefaults()
    {
        var sut = SpreadContextConfig.FromConfiguration(ConfigWith(null));

        sut.AtsKeyNumbers.Should().Equal(SpreadContextConfig.DefaultAtsKeyNumbers);
        sut.AtsBucketMaxDistancePoints.Should().Be(SpreadContextConfig.DefaultAtsBucketMaxDistancePoints);
    }

    [Fact]
    public void WhenLadderConfigured_ParsesSortsAndDeduplicates()
    {
        var sut = SpreadContextConfig.FromConfiguration(ConfigWith("14, 3.5, 7, 14"));

        sut.AtsKeyNumbers.Should().Equal(3.5, 7, 14);
    }

    [Theory]
    [InlineData("3,seven,10")]     // unparsable entry
    [InlineData("3,-7,10")]        // non-positive rung
    [InlineData("0")]              // zero rung
    [InlineData("3,Infinity,10")]  // NumberStyles.Float parses "Infinity"
    [InlineData("NaN")]            // and "NaN"
    [InlineData("  ")]             // whitespace only
    public void WhenLadderMalformed_RejectsWholeStringForDefaults(string ladder)
    {
        // One bad entry rejects the whole string — a spliced ladder (part
        // operator, part default) must never exist.
        var sut = SpreadContextConfig.FromConfiguration(ConfigWith(ladder));

        sut.AtsKeyNumbers.Should().Equal(SpreadContextConfig.DefaultAtsKeyNumbers);
    }

    [Fact]
    public void WhenGuardConfigured_ParsesIt_AndMalformedFallsBack()
    {
        SpreadContextConfig.FromConfiguration(ConfigWith(null, "10.5"))
            .AtsBucketMaxDistancePoints.Should().Be(10.5);
        SpreadContextConfig.FromConfiguration(ConfigWith(null, "not-a-number"))
            .AtsBucketMaxDistancePoints.Should().Be(SpreadContextConfig.DefaultAtsBucketMaxDistancePoints);
        SpreadContextConfig.FromConfiguration(ConfigWith(null, "-1"))
            .AtsBucketMaxDistancePoints.Should().Be(SpreadContextConfig.DefaultAtsBucketMaxDistancePoints);
        SpreadContextConfig.FromConfiguration(ConfigWith(null, "Infinity"))
            .AtsBucketMaxDistancePoints.Should().Be(SpreadContextConfig.DefaultAtsBucketMaxDistancePoints);
    }
}
