using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Producer.Application.Venues.Jobs;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Geo;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Venues.Jobs;

public class VenueGeoCodeJobTests : ProducerTestBase<VenueGeoCodeJob>
{
    [Fact]
    public async Task Execute_ResolvesTimeZones_ForGeocodedVenuesWithoutOne()
    {
        // Arrange
        var resolver = Mocker.GetMock<IGeoTimeZoneResolver>();
        resolver.Setup(x => x.Resolve(40.8206m, -96.7056m)).Returns("America/Chicago");

        var missing = CreateVenue("memorial-stadium", 40.8206m, -96.7056m, timeZone: null);
        var alreadySet = CreateVenue("doak-campbell", 30.4381m, -84.3044m, timeZone: "America/New_York");
        await FootballDataContext.Venues.AddRangeAsync(missing, alreadySet);
        await FootballDataContext.SaveChangesAsync();

        // Act
        await Mocker.CreateInstance<VenueGeoCodeJob>().ExecuteAsync();

        // Assert
        var venues = await FootballDataContext.Venues.AsNoTracking().ToDictionaryAsync(v => v.Id);
        venues[missing.Id].TimeZone.Should().Be("America/Chicago");
        venues[alreadySet.Id].TimeZone.Should().Be("America/New_York");
        resolver.Verify(x => x.Resolve(It.IsAny<decimal>(), It.IsAny<decimal>()), Times.Once);
    }

    [Fact]
    public async Task Execute_LeavesUngeocodedVenuesForGeocoding()
    {
        // Arrange
        var ungeocoded = CreateVenue("unknown-stadium", 0m, 0m, timeZone: null);
        await FootballDataContext.Venues.AddAsync(ungeocoded);
        await FootballDataContext.SaveChangesAsync();

        // Act
        await Mocker.CreateInstance<VenueGeoCodeJob>().ExecuteAsync();

        // Assert
        Mocker.GetMock<IGeoTimeZoneResolver>().Verify(
            x => x.Resolve(It.IsAny<decimal>(), It.IsAny<decimal>()), Times.Never);
        (await FootballDataContext.Venues.AsNoTracking().FirstAsync(v => v.Id == ungeocoded.Id))
            .TimeZone.Should().BeNull();
    }

    private Venue CreateVenue(string slug, decimal latitude, decimal longitude, string timeZone)
    {
        return Fixture.Build<Venue>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.Name, slug)
            .With(x => x.Slug, slug)
            .With(x => x.City, "City")
            .With(x => x.State, "ST")
            .With(x => x.PostalCode, "00000")
            .With(x => x.Country, "USA")
            .With(x => x.Latitude, latitude)
            .With(x => x.Longitude, longitude)
            .With(x => x.TimeZone, timeZone)
            .Create();
    }
}
