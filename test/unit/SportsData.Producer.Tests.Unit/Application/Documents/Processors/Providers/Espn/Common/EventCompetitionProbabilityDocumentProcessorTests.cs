using AutoFixture;

using MassTransit;

using SportsData.Core.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Common;

public class EventCompetitionProbabilityDocumentProcessorTests :
    ProducerTestBase<EventCompetitionProbabilityDocumentProcessor<TeamSportDataContext>>
{
    [Fact]
    public async Task WhenEntityDoesNotExist_IsAdded()
    {
        // arrange
        var bus = Mocker.GetMock<IPublishEndpoint>();

        var sut = Mocker.CreateInstance<EventCompetitionProbabilityDocumentProcessor<TeamSportDataContext>>();

        var documentJson = await LoadJsonTestData("EspnFootballNcaaEventCompetitionProbability.json");

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.DocumentType, DocumentType.EventCompetitionProbability)
            .With(x => x.Document, documentJson)
            .OmitAutoProperties()
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