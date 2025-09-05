using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;
using SportsData.Producer.Infrastructure.Data.Football;

using Xunit;

namespace SportsData.Producer.Tests.Unit.Application.Documents.Processors.Providers.Espn.Football;

public class FootballSeasonRankingDocumentProcessorTests : ProducerTestBase<FootballDataContext>
{
    [Fact]
    public async Task ProcessAsync_HappyPath_CreatesSeasonPollAndPublishesRankingRequests()
    {
        // Arrange
        var bus = Mocker.GetMock<IEventBus>();

        var json = await LoadJsonTestData("EspnFootballNcaaSeasonPoll.json");
        var dto = json.FromJson<EspnFootballSeasonRankingDto>();
        
        var generator = new ExternalRefIdentityGenerator();
        Mocker.Use<IGenerateExternalRefIdentities>(generator);

        var dtoIdentity =generator.Generate(dto!.Ref);

        var command = new ProcessDocumentCommand(
            SourceDataProvider.Espn,
            Sport.FootballNcaa,
            2025,
            DocumentType.SeasonTypeWeekRankings,
            json,
            Guid.NewGuid(),
            parentId: null,
            sourceUri: dto.Ref,
            urlHash: dtoIdentity.UrlHash
        );

        var sut = Mocker.CreateInstance<FootballSeasonRankingDocumentProcessor<FootballDataContext>>();

        // Act
        await sut.ProcessAsync(command);

        // Assert
        var poll = await FootballDataContext.SeasonPolls.Include(x => x.ExternalIds).FirstOrDefaultAsync();
        poll.Should().NotBeNull();
        poll!.Name.Should().Be(dto.Name);
        poll.ShortName.Should().Be(dto.ShortName);
        poll.SeasonYear.Should().Be(2025);
        poll.ExternalIds.Should().ContainSingle();

        bus.Verify(x => x.Publish(It.IsAny<DocumentRequested>(), It.IsAny<CancellationToken>()), Times.Exactly(dto!.Rankings.Count));
    }
}
