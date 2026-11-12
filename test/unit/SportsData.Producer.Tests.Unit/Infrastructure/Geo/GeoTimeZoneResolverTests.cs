using FluentAssertions;

using SportsData.Producer.Infrastructure.Geo;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Infrastructure.Geo;

/// <summary>
/// Real lookups against the embedded boundary data. The split-state cases are
/// the reason for a coordinate lookup over a state map.
/// </summary>
public class GeoTimeZoneResolverTests
{
    private readonly GeoTimeZoneResolver _sut = new();

    [Theory]
    [InlineData(40.8206, -96.7056, "America/Chicago")]                // Lincoln, NE
    [InlineData(39.7601, -86.1639, "America/Indiana/Indianapolis")]   // Indianapolis, IN
    [InlineData(30.4381, -84.3044, "America/New_York")]               // Tallahassee, FL
    [InlineData(30.5490, -87.2190, "America/Chicago")]                // Pensacola, FL (panhandle)
    [InlineData(38.0221, -84.5054, "America/New_York")]               // Lexington, KY
    [InlineData(36.9850, -86.4590, "America/Chicago")]                // Bowling Green, KY
    [InlineData(33.4264, -111.9325, "America/Phoenix")]               // Tempe, AZ (no DST)
    [InlineData(21.3727, -157.9302, "Pacific/Honolulu")]              // Honolulu, HI
    [InlineData(53.3352, -6.2285, "Europe/Dublin")]                   // Dublin, Ireland
    public void Resolve_ReturnsIanaZone(double latitude, double longitude, string expected)
    {
        _sut.Resolve((decimal)latitude, (decimal)longitude).Should().Be(expected);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(40.8206, 0)]
    [InlineData(0, -96.7056)]
    public void Resolve_WhenCoordinateUnset_ReturnsNull(double latitude, double longitude)
    {
        _sut.Resolve((decimal)latitude, (decimal)longitude).Should().BeNull();
    }
}
