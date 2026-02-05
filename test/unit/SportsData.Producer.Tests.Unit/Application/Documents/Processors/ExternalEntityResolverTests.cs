using AutoFixture;

using FluentAssertions;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using SportsData.Producer.Infrastructure.Data.Common;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors;

public class ExternalEntityResolverTests : ProducerTestBase<ExternalEntityResolverTests>
{
    #region ResolveIdAsync - Single Resolve Tests

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
        result.Should().NotBeNull();
        result.Should().Be(venue.Id);
    }

    [Fact]
    public async Task ResolveIdAsync_ReturnsNull_WhenDtoRefIsNull()
    {
        // Arrange
        IHasRef? dtoRef = null;

        // Act
        var result = await FootballDataContext.ResolveIdAsync<Venue, VenueExternalId>(
            dtoRef!,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveIdAsync_ReturnsNull_WhenDtoRefHasNullRef()
    {
        // Arrange
        var dtoRef = new EspnResourceIndexItem { Ref = null!, Id = "123" };

        // Act
        var result = await FootballDataContext.ResolveIdAsync<Venue, VenueExternalId>(
            dtoRef,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveIdAsync_ReturnsNull_WhenNoMatchingHash()
    {
        // Arrange
        var venue = Fixture.Build<Venue>()
            .With(v => v.Id, Guid.NewGuid())
            .With(v => v.ExternalIds, new List<VenueExternalId>())
            .Create();

        var existingUrl = new Uri("http://espn.com/venues/123");
        var existingHash = HashProvider.GenerateHashFromUri(existingUrl);

        venue.ExternalIds.Add(new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            SourceUrlHash = existingHash,
            Value = existingHash,
            VenueId = venue.Id,
            SourceUrl = existingUrl.ToCleanUrl()
        });

        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        // Try to resolve with a different URL
        var differentUrl = new Uri("http://espn.com/venues/999");
        var dtoRef = new EspnResourceIndexItem { Ref = differentUrl, Id = "999" };

        // Act
        var result = await FootballDataContext.ResolveIdAsync<Venue, VenueExternalId>(
            dtoRef,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveIdAsync_ReturnsNull_WhenProviderDoesNotMatch()
    {
        // Arrange
        var venue = Fixture.Build<Venue>()
            .With(v => v.Id, Guid.NewGuid())
            .With(v => v.ExternalIds, new List<VenueExternalId>())
            .Create();

        var refUrl = new Uri("http://espn.com/venues/123");
        var hash = HashProvider.GenerateHashFromUri(refUrl);

        venue.ExternalIds.Add(new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Yahoo, // Different provider
            SourceUrlHash = hash,
            Value = hash,
            VenueId = venue.Id,
            SourceUrl = refUrl.ToCleanUrl()
        });

        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        var dtoRef = new EspnResourceIndexItem { Ref = refUrl, Id = "123" };

        // Act - looking for ESPN provider
        var result = await FootballDataContext.ResolveIdAsync<Venue, VenueExternalId>(
            dtoRef,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().BeNull("provider filter should exclude non-matching providers");
    }

    [Fact]
    public async Task ResolveIdAsync_ReturnsCorrectId_WhenMultipleEntitiesExist()
    {
        // Arrange
        var venue1 = CreateVenueWithExternalId("http://espn.com/venues/1");
        var venue2 = CreateVenueWithExternalId("http://espn.com/venues/2");
        var venue3 = CreateVenueWithExternalId("http://espn.com/venues/3");

        FootballDataContext.Venues.AddRange(venue1, venue2, venue3);
        await FootballDataContext.SaveChangesAsync();

        var dtoRef = new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/2"), Id = "2" };

        // Act
        var result = await FootballDataContext.ResolveIdAsync<Venue, VenueExternalId>(
            dtoRef,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().Be(venue2.Id, "should resolve to the correct entity");
    }

    [Fact]
    public async Task ResolveIdAsync_UsesCustomKeySelector_WhenProvided()
    {
        // Arrange
        var venue = CreateVenueWithExternalId("http://espn.com/venues/123");
        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        var dtoRef = new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/123"), Id = "123" };

        // Act - using custom key selector
        var result = await FootballDataContext.ResolveIdAsync<Venue, VenueExternalId>(
            dtoRef,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues,
            key: v => v.Id);

        // Assert
        result.Should().Be(venue.Id);
    }

    #endregion

    #region ResolveIdsAsync - Batch Resolve Tests

    [Fact]
    public async Task ResolveIdsAsync_ReturnsEmptyDictionary_WhenNoRefsProvided()
    {
        // Arrange
        var emptyRefs = new List<IHasRef>();

        // Act
        var result = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            emptyRefs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveIdsAsync_ReturnsEmptyDictionary_WhenAllRefsAreNull()
    {
        // Arrange
        var refs = new List<IHasRef>
        {
            new EspnResourceIndexItem { Ref = null!, Id = "1" },
            new EspnResourceIndexItem { Ref = null!, Id = "2" }
        };

        // Act
        var result = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            refs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveIdsAsync_ResolvesMultipleRefs_Successfully()
    {
        // Arrange
        var venue1 = CreateVenueWithExternalId("http://espn.com/venues/1");
        var venue2 = CreateVenueWithExternalId("http://espn.com/venues/2");
        var venue3 = CreateVenueWithExternalId("http://espn.com/venues/3");

        FootballDataContext.Venues.AddRange(venue1, venue2, venue3);
        await FootballDataContext.SaveChangesAsync();

        var refs = new List<IHasRef>
        {
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/1"), Id = "1" },
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/2"), Id = "2" },
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/3"), Id = "3" }
        };

        // Act
        var result = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            refs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().HaveCount(3);
        result[HashProvider.GenerateHashFromUri(new Uri("http://espn.com/venues/1"))].Should().Be(venue1.Id);
        result[HashProvider.GenerateHashFromUri(new Uri("http://espn.com/venues/2"))].Should().Be(venue2.Id);
        result[HashProvider.GenerateHashFromUri(new Uri("http://espn.com/venues/3"))].Should().Be(venue3.Id);
    }

    [Fact]
    public async Task ResolveIdsAsync_HandlesDuplicateRefs_ByDeduplicating()
    {
        // Arrange
        var venue = CreateVenueWithExternalId("http://espn.com/venues/123");
        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        var refs = new List<IHasRef>
        {
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/123"), Id = "123" },
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/123"), Id = "123" },
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/123"), Id = "123" }
        };

        // Act
        var result = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            refs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().HaveCount(1, "duplicate refs should be deduplicated");
        var hash = HashProvider.GenerateHashFromUri(new Uri("http://espn.com/venues/123"));
        result[hash].Should().Be(venue.Id);
    }

    [Fact]
    public async Task ResolveIdsAsync_FiltersOutNullRefs()
    {
        // Arrange
        var venue = CreateVenueWithExternalId("http://espn.com/venues/123");
        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        var refs = new List<IHasRef>
        {
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/123"), Id = "123" },
            new EspnResourceIndexItem { Ref = null!, Id = "456" },
            null!
        };

        // Act
        var result = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            refs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().HaveCount(1, "only the valid ref should be resolved");
    }

    [Fact]
    public async Task ResolveIdsAsync_ReturnsPartialMatches_WhenSomeRefsNotFound()
    {
        // Arrange
        var venue1 = CreateVenueWithExternalId("http://espn.com/venues/1");
        var venue2 = CreateVenueWithExternalId("http://espn.com/venues/2");

        FootballDataContext.Venues.AddRange(venue1, venue2);
        await FootballDataContext.SaveChangesAsync();

        var refs = new List<IHasRef>
        {
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/1"), Id = "1" },
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/2"), Id = "2" },
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/999"), Id = "999" } // Doesn't exist
        };

        // Act
        var result = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            refs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().HaveCount(2, "only the found venues should be in the result");
        result.Should().ContainKey(HashProvider.GenerateHashFromUri(new Uri("http://espn.com/venues/1")));
        result.Should().ContainKey(HashProvider.GenerateHashFromUri(new Uri("http://espn.com/venues/2")));
        result.Should().NotContainKey(HashProvider.GenerateHashFromUri(new Uri("http://espn.com/venues/999")));
    }

    [Fact]
    public async Task ResolveIdsAsync_FiltersByProvider_WhenMultipleProvidersExist()
    {
        // Arrange
        var venue = Fixture.Build<Venue>()
            .With(v => v.Id, Guid.NewGuid())
            .With(v => v.ExternalIds, new List<VenueExternalId>())
            .Create();

        var url = new Uri("http://espn.com/venues/123");
        var hash = HashProvider.GenerateHashFromUri(url);

        // Add external IDs from multiple providers with same hash
        venue.ExternalIds.Add(new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            SourceUrlHash = hash,
            Value = hash,
            VenueId = venue.Id,
            SourceUrl = url.ToCleanUrl()
        });

        venue.ExternalIds.Add(new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Yahoo,
            SourceUrlHash = hash,
            Value = hash,
            VenueId = venue.Id,
            SourceUrl = url.ToCleanUrl()
        });

        FootballDataContext.Venues.Add(venue);
        await FootballDataContext.SaveChangesAsync();

        var refs = new List<IHasRef>
        {
            new EspnResourceIndexItem { Ref = url, Id = "123" }
        };

        // Act - resolve for ESPN provider only
        var result = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            refs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result.Should().HaveCount(1);
        result[hash].Should().Be(venue.Id);
    }

    [Fact]
    public async Task ResolveIdsAsync_ReturnsDeterministicOrder_WhenHashesAreOrdered()
    {
        // Arrange
        var venue1 = CreateVenueWithExternalId("http://espn.com/venues/3");
        var venue2 = CreateVenueWithExternalId("http://espn.com/venues/1");
        var venue3 = CreateVenueWithExternalId("http://espn.com/venues/2");

        FootballDataContext.Venues.AddRange(venue1, venue2, venue3);
        await FootballDataContext.SaveChangesAsync();

        var refs = new List<IHasRef>
        {
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/3"), Id = "3" },
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/1"), Id = "1" },
            new EspnResourceIndexItem { Ref = new Uri("http://espn.com/venues/2"), Id = "2" }
        };

        // Act - call multiple times to verify deterministic behavior
        var result1 = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            refs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        var result2 = await FootballDataContext.ResolveIdsAsync<Venue, VenueExternalId>(
            refs,
            SourceDataProvider.Espn,
            () => FootballDataContext.Venues);

        // Assert
        result1.Keys.Should().BeEquivalentTo(result2.Keys, "results should be deterministic");
        result1.Should().HaveCount(3);
    }

    #endregion

    #region Helper Methods

    private Venue CreateVenueWithExternalId(string refUrl)
    {
        var venue = Fixture.Build<Venue>()
            .With(v => v.Id, Guid.NewGuid())
            .With(v => v.ExternalIds, new List<VenueExternalId>())
            .Create();

        var uri = new Uri(refUrl);
        var hash = HashProvider.GenerateHashFromUri(uri);

        venue.ExternalIds.Add(new VenueExternalId
        {
            Id = Guid.NewGuid(),
            Provider = SourceDataProvider.Espn,
            SourceUrlHash = hash,
            Value = hash,
            VenueId = venue.Id,
            SourceUrl = uri.ToCleanUrl()
        });

        return venue;
    }

    #endregion
}
