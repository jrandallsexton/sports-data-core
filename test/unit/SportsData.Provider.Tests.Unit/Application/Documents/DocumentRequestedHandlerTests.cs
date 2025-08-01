using AutoFixture;

using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Documents;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Providers.Espn;
using SportsData.Tests.Shared;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Documents;

public class DocumentRequestedHandlerTests : UnitTestBase<DocumentRequestedHandler>
{
    [Fact]
    public async Task WhenDocumentIsIndex_EmitsDocumentRequestedEvents()
    {
        // arrange
        var json = await LoadJsonTestData("EspnAwardsIndex.json"); // contains EspnResourceIndexDto with Items

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        var publisher = Mocker.GetMock<IPublishEndpoint>();

        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), false)).ReturnsAsync(json);

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, new Uri("https://sports.core.api.espn.com/v2/awards/index"))
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
            d.SourceDataProvider == SourceDataProvider.Espn &&
            !string.IsNullOrWhiteSpace(d.Uri.ToCleanUrl())), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenDocumentIsNotIndex_EnqueuesProcessCommand()
    {
        // arrange
        var json = await LoadJsonTestData("EspnTeamBySeason.json"); // NOT a resource index

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), false)).ReturnsAsync(json);

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, new Uri("https://sports.core.api.espn.com/v2/teams/99"))
            .With(x => x.DocumentType, DocumentType.TeamSeason)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        await handler.Consume(ctx);

        // assert
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.Once);
    }

    [Fact]
    public async Task WhenDocumentIsInvalid_LogsAndReturnsWithoutException()
    {
        // arrange
        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), false)).ReturnsAsync("invalid json");

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, new Uri("https://sports.core.api.espn.com/v2/teams/99"))
            .With(x => x.DocumentType, DocumentType.TeamSeason)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        var exception = await Record.ExceptionAsync(() => handler.Consume(ctx));

        // assert
        Assert.Null(exception); // handled internally
    }

    [Fact]
    public async Task WhenPagedIndex_ProcessesAllPages()
    {
        // arrange
        var page1 = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlays_Page0.json");
        var page2 = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlays_Page1.json");

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        var publisher = Mocker.GetMock<IPublishEndpoint>();
        var jobs = Mocker.GetMock<IProvideBackgroundJobs>();

        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true))
            .ReturnsAsync((Uri uri, bool strip) =>
            {
                if (uri.ToString().Contains("page=2"))
                    return page2;
                return page1; // default to page1 for any other input
            });

        var uriPage1 = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628420/competitions/401628420/plays?lang=en&region=us&page=1&limit=25");
        var uriPage2 = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628420/competitions/401628420/plays?limit=25&page=2");

        espnApi.Setup(x => x.GetResource(uriPage1, false)).ReturnsAsync(page1);
        espnApi.Setup(x => x.GetResource(uriPage2, false)).ReturnsAsync(page2);

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628420/competitions/401628420/plays?lang=en&region=us&page=1&limit=25"))
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        await handler.Consume(ctx);

        // assert
        jobs.Verify(x =>
                x.Enqueue<IProcessResourceIndexItems>(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()),
            Times.AtLeast(50));


    }
}
