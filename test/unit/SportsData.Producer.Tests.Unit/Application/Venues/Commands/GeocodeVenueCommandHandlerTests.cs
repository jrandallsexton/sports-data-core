using AutoFixture;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Producer.Application.Venues.Commands.GeocodeVenue;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Geo;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Venues.Commands;

public class GeocodeVenueCommandHandlerTests :
    ProducerTestBase<GeocodeVenueCommandHandler>
{
    [Fact]
    public async Task WhenVenueExistsAndGeocodeSucceeds_ShouldUpdateVenueCoordinates()
    {
        // Arrange
        var geocodingService = Mocker.GetMock<IGeocodingService>();
        geocodingService
            .Setup(x => x.TryGeocodeAsync(It.IsAny<string>()))
            .ReturnsAsync((40.8128, -96.7026));

        var sut = Mocker.CreateInstance<GeocodeVenueCommandHandler>();

        var venueId = Guid.NewGuid();
        var venue = CreateVenue(venueId, "Memorial Stadium", "Lincoln", "NE", "68588");

        await FootballDataContext.Venues.AddAsync(venue);
        await FootballDataContext.SaveChangesAsync();

        var command = new GeocodeVenueCommand(venueId);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(venueId);

        var updatedVenue = await FootballDataContext.Venues.FirstOrDefaultAsync(v => v.Id == venueId);
        updatedVenue.Should().NotBeNull();
        updatedVenue!.Latitude.Should().Be(40.8128m);
        updatedVenue.Longitude.Should().Be(-96.7026m);
    }

    [Fact]
    public async Task WhenVenueDoesNotExist_ShouldReturnSuccessWithoutUpdate()
    {
        // Arrange
        var geocodingService = Mocker.GetMock<IGeocodingService>();
        var sut = Mocker.CreateInstance<GeocodeVenueCommandHandler>();

        var nonExistentId = Guid.NewGuid();
        var command = new GeocodeVenueCommand(nonExistentId);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(nonExistentId);

        geocodingService.Verify(
            x => x.TryGeocodeAsync(It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task WhenGeocodeFails_ShouldReturnSuccessWithoutUpdatingCoordinates()
    {
        // Arrange
        var geocodingService = Mocker.GetMock<IGeocodingService>();
        geocodingService
            .Setup(x => x.TryGeocodeAsync(It.IsAny<string>()))
            .ReturnsAsync(((double?)null, (double?)null));

        var sut = Mocker.CreateInstance<GeocodeVenueCommandHandler>();

        var venueId = Guid.NewGuid();
        var venue = CreateVenue(venueId, "Unknown Stadium", "Unknown", "XX", "00000");

        await FootballDataContext.Venues.AddAsync(venue);
        await FootballDataContext.SaveChangesAsync();

        var command = new GeocodeVenueCommand(venueId);

        // Act
        var result = await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var unchangedVenue = await FootballDataContext.Venues.FirstOrDefaultAsync(v => v.Id == venueId);
        unchangedVenue.Should().NotBeNull();
        unchangedVenue!.Latitude.Should().Be(0m);
        unchangedVenue.Longitude.Should().Be(0m);
    }

    [Fact]
    public async Task WhenGeocoding_ShouldBuildCorrectAddressString()
    {
        // Arrange
        string capturedAddress = string.Empty;
        var geocodingService = Mocker.GetMock<IGeocodingService>();
        geocodingService
            .Setup(x => x.TryGeocodeAsync(It.IsAny<string>()))
            .Callback<string>(addr => capturedAddress = addr)
            .ReturnsAsync((35.1234, -97.5678));

        var sut = Mocker.CreateInstance<GeocodeVenueCommandHandler>();

        var venueId = Guid.NewGuid();
        var venue = CreateVenue(venueId, "Gaylord Family Oklahoma Memorial Stadium", "Norman", "OK", "73019");

        await FootballDataContext.Venues.AddAsync(venue);
        await FootballDataContext.SaveChangesAsync();

        var command = new GeocodeVenueCommand(venueId);

        // Act
        await sut.ExecuteAsync(command, CancellationToken.None);

        // Assert
        capturedAddress.Should().Be("Gaylord Family Oklahoma Memorial Stadium, Norman, OK, 73019, USA");
    }

    private Venue CreateVenue(Guid id, string name, string city, string state, string postalCode)
    {
        return Fixture.Build<Venue>()
            .OmitAutoProperties()
            .With(x => x.Id, id)
            .With(x => x.Name, name)
            .With(x => x.Slug, name.ToLower().Replace(" ", "-"))
            .With(x => x.City, city)
            .With(x => x.State, state)
            .With(x => x.PostalCode, postalCode)
            .With(x => x.Country, "USA")
            .With(x => x.Latitude, 0m)
            .With(x => x.Longitude, 0m)
            .Create();
    }
}
