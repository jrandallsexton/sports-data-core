using AutoFixture;

using FluentAssertions;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class EventCompetitionCompetitorRosterDocumentProcessorTests
    : ProducerTestBase<EventCompetitionCompetitorRosterDocumentProcessor<FootballDataContext>>
{
    [Fact]
    public async Task WhenJsonIsValid_DtoDeserializes()
    {
        // arrange
        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorRoster.json");

        // act
        var dto = json.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        // assert
        dto.Should().NotBeNull();
        dto!.Ref.Should().NotBeNull();
        dto.Entries.Should().HaveCount(111);
        dto.Entries.Count(e => e.Statistics?.Ref != null).Should().Be(39);
        dto.Competition.Should().NotBeNull();
        dto.Team.Should().NotBeNull();
    }

    [Fact]
    public async Task WhenProcessingRoster_PublishesChildDocumentRequestsForAthleteStatistics()
    {
        // arrange
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var bus = Mocker.GetMock<IEventBus>();
        var sut = Mocker.CreateInstance<EventCompetitionCompetitorRosterDocumentProcessor<FootballDataContext>>();

        var json = await LoadJsonTestData("EspnFootballNcaaEventCompetitionCompetitorRoster.json");
        var competitorId = Guid.NewGuid();

        var command = Fixture.Build<ProcessDocumentCommand>()
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.FootballNcaa)
            .With(x => x.Season, 2025)
            .With(x => x.DocumentType, DocumentType.EventCompetitionCompetitorRoster)
            .With(x => x.Document, json)
            .With(x => x.ParentId, competitorId.ToString())
            .Create();

        // act
        await sut.ProcessAsync(command);

        // assert - processor should complete successfully and publish 39 DocumentRequested events
        // (PublishChildDocumentRequest publishes DocumentRequested, not DocumentCreated)
        bus.Verify(x => x.Publish(
            It.Is<DocumentRequested>(d => d.DocumentType == DocumentType.EventCompetitionAthleteStatistics),
            It.IsAny<CancellationToken>()), 
            Times.Exactly(39));
    }
}
