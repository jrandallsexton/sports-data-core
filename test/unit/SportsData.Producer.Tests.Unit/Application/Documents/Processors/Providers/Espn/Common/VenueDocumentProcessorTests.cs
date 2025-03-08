using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Venues;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    public class VenueDocumentProcessorTests : UnitTestBase<VenueDocumentProcessorTests>
    {
        [Fact]
        public async Task WhenEntityDoesNotExist_IsAdded()
        {
            // arrange
            var bus = Mocker.GetMock<IPublishEndpoint>();

            var sut = Mocker.CreateInstance<VenueDocumentProcessor>();

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

        }
    }
}
