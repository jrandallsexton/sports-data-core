using AutoFixture;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Infrastructure.Data.Common;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors;

public class ExternalEntityResolverTests : ProducerTestBase<ExternalEntityResolverTests>
{
    [Fact]
    public async Task ResolveIdAsync_ReturnsId_WhenUrlHashMatches()
    {
        // Arrange
        var venue = Fixture.Build<Venue>()
            .With(x => x.Id, Guid.NewGuid)
            .With(v => v.ExternalIds, new List<VenueExternalId>())
            .Create();

        var refUrl = new Uri("http://espn.com/venues/123");
        var hash = HashProvider.GenerateHashFromUri(refUrl);

        var externalId = new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            SourceUrlHash = hash,
            Value = hash,
            Venue = venue,
            VenueId = venue.Id,
            SourceUrl = refUrl.ToCleanUrl()
        };

        venue.ExternalIds.Add(externalId);
        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        // Use a dto that implements IHasRef (resolver expects this)
        var dtoRef = new EspnResourceIndexItem { Ref = refUrl, Id = "123" };

        // Act
        var result = await FootballDataContext.ResolveIdAsync<Venue, VenueExternalId>(
            dtoRef,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues,
            externalIdsNav: "ExternalIds",
            key: v => v.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(venue.Id, result);
    }

    [Fact]
    public async Task ResolveIdAsync_ReturnsId_WhenRefMatches()
    {
        // Arrange
        var venue = Fixture.Build<Venue>()
            .With(v => v.Id, Guid.NewGuid())
            .With(v => v.ExternalIds, new List<VenueExternalId>())
            .Create();

        var refUrl = new Uri("http://espn.com/venues/456");
        var hash = HashProvider.GenerateHashFromUri(refUrl);

        var externalId = new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            SourceUrlHash = hash,
            Value = hash,
            Venue = venue,
            VenueId = venue.Id,
            SourceUrl = refUrl.ToCleanUrl()
        };

        venue.ExternalIds.Add(externalId);
        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        var dtoRef = new EspnResourceIndexItem { Ref = refUrl, Id = "456" };

        // Act
        var result = await FootballDataContext.ResolveIdAsync<Venue, VenueExternalId>(
            dtoRef,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues,
            externalIdsNav: "ExternalIds",
            key: v => v.Id);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(venue.Id, result);
    }
}
