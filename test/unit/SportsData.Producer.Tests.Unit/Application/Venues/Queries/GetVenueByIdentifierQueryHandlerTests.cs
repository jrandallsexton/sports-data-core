using AutoFixture;

using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Venues.Queries.GetVenueByIdentifier;
using SportsData.Producer.Infrastructure.Data.Common;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Venues.Queries;

public class GetVenueByIdentifierQueryHandlerTests :
    ProducerTestBase<GetVenueByIdQueryHandler>
{
    [Fact]
    public async Task WhenVenueExistsById_ShouldReturnSuccessWithVenue()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetVenueByIdQueryHandler>();

        var venueId = Guid.NewGuid();
        var venue = CreateVenue("Test Stadium", "test-stadium", venueId);

        await FootballDataContext.Venues.AddAsync(venue);
        await FootballDataContext.SaveChangesAsync();

        var query = new GetVenueByIdQuery(venueId.ToString());

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<VenueDto>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Test Stadium");
        result.Value.Slug.Should().Be("test-stadium");
    }

    [Fact]
    public async Task WhenVenueExistsBySlug_ShouldReturnSuccessWithVenue()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetVenueByIdQueryHandler>();

        var venue = CreateVenue("Memorial Stadium", "memorial-stadium");
        venue.Capacity = 90000;

        await FootballDataContext.Venues.AddAsync(venue);
        await FootballDataContext.SaveChangesAsync();

        var query = new GetVenueByIdQuery("memorial-stadium");

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<VenueDto>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.Name.Should().Be("Memorial Stadium");
        result.Value.Capacity.Should().Be(90000);
    }

    [Fact]
    public async Task WhenVenueDoesNotExistById_ShouldReturnFailureNotFound()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetVenueByIdQueryHandler>();
        var nonExistentId = Guid.NewGuid();
        var query = new GetVenueByIdQuery(nonExistentId.ToString());

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Failure<VenueDto>>();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    [Fact]
    public async Task WhenVenueDoesNotExistBySlug_ShouldReturnFailureNotFound()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetVenueByIdQueryHandler>();
        var query = new GetVenueByIdQuery("non-existent-stadium");

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Failure<VenueDto>>();
        result.IsSuccess.Should().BeFalse();
        result.Status.Should().Be(ResultStatus.NotFound);
    }

    private Venue CreateVenue(string name, string slug, Guid? id = null)
    {
        return Fixture.Build<Venue>()
            .OmitAutoProperties()
            .With(x => x.Id, id ?? Guid.NewGuid())
            .With(x => x.Name, name)
            .With(x => x.Slug, slug)
            .With(x => x.City, "Test City")
            .With(x => x.State, "TS")
            .With(x => x.PostalCode, "12345")
            .With(x => x.Country, "USA")
            .With(x => x.Capacity, 80000)
            .With(x => x.IsGrass, true)
            .With(x => x.IsIndoor, false)
            .With(x => x.Images, new List<VenueImage>())
            .Create();
    }
}
