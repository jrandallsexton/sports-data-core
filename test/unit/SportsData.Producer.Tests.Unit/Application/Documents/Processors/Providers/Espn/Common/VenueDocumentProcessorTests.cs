using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    public class VenueDocumentProcessorTests : ProducerTestBase<VenueDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenEntityDoesNotExist_IsAdded()
        {
            // arrange
            var bus = Mocker.GetMock<IPublishEndpoint>();

            var sut = Mocker.CreateInstance<VenueDocumentProcessor<FootballDataContext>>();

            var documentJson = await LoadJsonTestData("EspnFootballNflVenue.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.Venue)
                .With(x => x.Document, documentJson)
                .OmitAutoProperties()
                .Create();

            // act
            await sut.ProcessAsync(command);

            // assert
            var venue = await base.FootballDataContext.Venues
                .AsNoTracking()
                .FirstOrDefaultAsync();

            venue.Should().NotBeNull();

            bus.Verify(x => x.Publish(It.IsAny<VenueCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WhenEntityExists_IsUpdated()
        {
            // Arrange
            var existingVenueId = Guid.NewGuid();
            var espnId = "3810";

            var originalVenue = new Venue
            {
                Id = existingVenueId,
                Name = "Old Name",
                City = "Old City",
                State = "TX",
                PostalCode = "00000",
                Country = "USA",
                Capacity = 40000,
                IsGrass = false,
                IsIndoor = false,
                Slug = "old-name",
                ShortName = "Old Name",
                ExternalIds =
                [
                    new VenueExternalId
                    {
                        Id = Guid.NewGuid(),
                        Provider = SourceDataProvider.Espn,
                        Value = espnId,
                        SourceUrlHash = "somehash"
                    }
                ],
                Images =
                [
                    new VenueImage
                    {
                        Id = Guid.NewGuid(),
                        VenueId = existingVenueId,
                        OriginalUrlHash = "existinghash",
                        Uri = new Uri("https://example.com/existing.jpg"),
                    }
                ]
            };

            FootballDataContext.Venues.Add(originalVenue);
            await FootballDataContext.SaveChangesAsync();

            var updatedJson = await LoadJsonTestData("EspnFootballNflVenue.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.Venue)
                .With(x => x.Document, updatedJson)
                .OmitAutoProperties()
                .Create();

            var bus = Mocker.GetMock<IPublishEndpoint>();

            var sut = Mocker.CreateInstance<VenueDocumentProcessor<FootballDataContext>>();

            // Act
            await sut.ProcessAsync(command);

            // Assert
            var updatedVenue = await FootballDataContext.Venues
                .Include(v => v.Images)
                .FirstAsync(v => v.Id == existingVenueId);

            updatedVenue.Name.Should().Be("Nissan Stadium"); // confirm at least one field was updated

            bus.Verify(x =>
                x.Publish(It.Is<VenueUpdated>(v => v.Canonical.Name == "Nissan Stadium"), It.IsAny<CancellationToken>()),
                Times.Once);
        }

    }
}
