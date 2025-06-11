using AutoFixture;

using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Provider.Application.Documents;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Providers.Espn;
using SportsData.Tests.Shared;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Documents
{
    public class DocumentRequestedHandlerTests : UnitTestBase<DocumentRequestedHandler>
    {
        [Fact]
        public async Task WhenDocumentIsIndex_EmitsDocumentRequestedEvents()
        {
            // arrange
            var json = await LoadJsonTestData("EspnAwardsIndex.json"); // file must include "items" array with "$ref"

            var espnApi = Mocker.GetMock<IProvideEspnApiData>();
            var publisher = Mocker.GetMock<IPublishEndpoint>();

            espnApi.Setup(x => x.GetResource(It.IsAny<string>(), true)).ReturnsAsync(json);

            var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

            var msg = Fixture.Build<DocumentRequested>()
                .With(x => x.Uri, new Uri("http://sports.core.api.espn.com/v2/awards/index"))
                .With(x => x.DocumentType, DocumentType.Award)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .OmitAutoProperties()
                .Create();

            var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

            // act
            await handler.Consume(ctx);

            // assert
            publisher.Verify(x => x.Publish(It.Is<DocumentRequested>(d =>
                d.DocumentType == DocumentType.Award &&
                !string.IsNullOrWhiteSpace(d.Uri.ToCleanUrl())), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

        [Fact]
        public async Task WhenDocumentIsNotIndex_EmitsProcessResourceIndexItemCommand()
        {
            // arrange
            var json = await LoadJsonTestData("EspnTeamBySeason.json"); // file must NOT include "items" array

            var espnApi = Mocker.GetMock<IProvideEspnApiData>();
            var publisher = Mocker.GetMock<IPublishEndpoint>();

            espnApi.Setup(x => x.GetResource(It.IsAny<string>(), true)).ReturnsAsync(json);

            var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

            var msg = Fixture.Build<DocumentRequested>()
                .With(x => x.Uri, new Uri("http://sports.core.api.espn.com/v2/teams/99"))
                .With(x => x.DocumentType, DocumentType.TeamBySeason)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .OmitAutoProperties()
                .Create();

            var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

            // act
            await handler.Consume(ctx);

            // assert
            publisher.Verify(x => x.Publish(It.Is<ProcessResourceIndexItemCommand>(d =>
                d.DocumentType == DocumentType.TeamBySeason &&
                d.Uri == msg.Uri), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task WhenDocumentIsHybridWithItems_EmitsDocumentRequestedEvents()
        {
            // arrange
            var json = await LoadJsonTestData("EspnTeamSeasonRecord.json"); // hybrid doc: top-level metadata + "items" with $refs

            var espnApi = Mocker.GetMock<IProvideEspnApiData>();
            var publisher = Mocker.GetMock<IPublishEndpoint>();

            espnApi.Setup(x => x.GetResource(It.IsAny<string>(), true)).ReturnsAsync(json);

            var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

            var msg = Fixture.Build<DocumentRequested>()
                .With(x => x.Uri, new Uri("http://sports.core.api.espn.com/v2/sports/football/college-football/teams/99/record"))
                .With(x => x.DocumentType, DocumentType.TeamSeasonRecord)
                .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
                .OmitAutoProperties()
                .Create();

            var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

            // act
            await handler.Consume(ctx);

            // assert
            publisher.Verify(x => x.Publish(It.Is<DocumentRequested>(d =>
                d.DocumentType == DocumentType.TeamSeasonRecord &&
                !string.IsNullOrWhiteSpace(d.Uri.ToCleanUrl())), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
        }

    }
}
