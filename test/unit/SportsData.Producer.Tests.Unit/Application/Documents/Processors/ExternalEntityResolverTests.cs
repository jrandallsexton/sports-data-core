using AutoFixture;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors;
using SportsData.Producer.Infrastructure.Data.Entities;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors;

public class ExternalEntityResolverTests : ProducerTestBase<ExternalEntityResolverTests>
{
    [Fact]
    public async Task TryResolveEntityIdAsync_ReturnsId_WhenUrlHashMatches()
    {
        // Arrange
        var venue = Fixture.Build<Venue>()
            .With(v => v.ExternalIds, new List<VenueExternalId>())
            .Create();

        var refUrl = new Uri("http://espn.com/venues/123");
        var hash = HashProvider.GenerateHashFromUri(refUrl);

        var externalId = new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            UrlHash = hash,
            Value = "123",
            Venue = venue
        };

        venue.ExternalIds.Add(externalId);
        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        // Act
        var result = await FootballDataContext.TryResolveEntityIdAsync(
            refUrl,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues,
            v => v.ExternalIds,
            null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(venue.Id, result);
    }

    [Fact]
    public async Task TryResolveFromDtoRefAsync_ReturnsId_WhenRefMatches()
    {
        // Arrange
        var venue = Fixture.Build<Venue>()
            .With(v => v.ExternalIds, new List<VenueExternalId>())
            .Create();

        var refUrl = new Uri("http://espn.com/venues/456");
        var hash = HashProvider.GenerateHashFromUri(refUrl);

        var externalId = new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            UrlHash = hash,
            Value = "456",
            Venue = venue
        };

        venue.ExternalIds.Add(externalId);
        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        var dtoRef = new EspnResourceIndexItem
        {
            Ref = refUrl,
            Id = 456
        };

        // Act
        var result = await FootballDataContext.TryResolveFromDtoRefAsync(
            dtoRef,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues,
            null);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(venue.Id, result);
    }
}