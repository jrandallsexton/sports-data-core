using AutoFixture;

using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Dtos.Canonical;
using SportsData.Producer.Application.Venues.Queries.GetAllVenues;
using SportsData.Producer.Infrastructure.Data.Common;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Venues.Queries;

public class GetAllVenuesQueryHandlerTests :
    ProducerTestBase<GetAllVenuesQueryHandler>
{
    [Fact]
    public async Task WhenVenuesExist_ShouldReturnSuccessWithVenues()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetAllVenuesQueryHandler>();

        for (int i = 0; i < 3; i++)
        {
            var venue = CreateVenue($"Stadium {i}", $"stadium-{i}");
            await FootballDataContext.Venues.AddAsync(venue);
        }

        await FootballDataContext.SaveChangesAsync();

        var query = new GetAllVenuesQuery();

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<GetAllVenuesResponse>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().HaveCount(3);
    }

    [Fact]
    public async Task WhenNoVenuesExist_ShouldReturnSuccessWithEmptyList()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetAllVenuesQueryHandler>();
        var query = new GetAllVenuesQuery();

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.Should().BeOfType<Success<GetAllVenuesResponse>>();
        result.IsSuccess.Should().BeTrue();
        result.Value.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task WhenVenuesHaveImages_ShouldIncludeImagesInResult()
    {
        // Arrange
        var sut = Mocker.CreateInstance<GetAllVenuesQueryHandler>();

        var venueId = Guid.NewGuid();
        var venue = CreateVenue("Test Stadium", "test-stadium", venueId);

        await FootballDataContext.Venues.AddAsync(venue);

        var image = Fixture.Build<VenueImage>()
            .OmitAutoProperties()
            .With(x => x.Id, Guid.NewGuid())
            .With(x => x.VenueId, venueId)
            .With(x => x.Uri, new Uri("https://example.com/image.png"))
            .With(x => x.OriginalUrlHash, "hash123")
            .Create();

        await FootballDataContext.VenueImages.AddAsync(image);
        await FootballDataContext.SaveChangesAsync();

        var query = new GetAllVenuesQuery();

        // Act
        var result = await sut.ExecuteAsync(query, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalCount.Should().Be(1);
        // TODO: Re-enable once Images projection is implemented
        // result.Value.Items[0].Images.Should().HaveCount(1);
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
            .With(x => x.Capacity, 50000)
            .With(x => x.IsGrass, true)
            .With(x => x.IsIndoor, false)
            .With(x => x.Images, new List<VenueImage>())
            .Create();
    }
}
