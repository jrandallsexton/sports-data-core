﻿using AutoFixture;

using MassTransit;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common
{
    public class EventCompetitionDocumentProcessorTests :
        ProducerTestBase<EventCompetitionDocumentProcessor<FootballDataContext>>
    {
        [Fact]
        public async Task WhenEntityDoesNotExist_IsAdded()
        {
            // arrange
            var bus = Mocker.GetMock<IPublishEndpoint>();

            // TODO: Create the parent event entity before processing the competition document

            var sut = Mocker.CreateInstance<EventCompetitionDocumentProcessor<FootballDataContext>>();

            var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetition.json");

            var command = Fixture.Build<ProcessDocumentCommand>()
                .OmitAutoProperties()
                .With(x => x.ParentId, string.Empty)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .With(x => x.Sport, Sport.FootballNcaa)
                .With(x => x.DocumentType, DocumentType.EventCompetition)
                .With(x => x.Document, documentJson)
                .With(x => x.UrlHash, "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334?lang=en".UrlHash())
                .Create();

            // act
            await sut.ProcessAsync(command);

            //// assert
            //var venue = await base.FootballDataContext.Venues
            //    .AsNoTracking()
            //    .FirstOrDefaultAsync();

            //venue.Should().NotBeNull();

            //bus.Verify(x => x.Publish(It.IsAny<VenueCreated>(), It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
