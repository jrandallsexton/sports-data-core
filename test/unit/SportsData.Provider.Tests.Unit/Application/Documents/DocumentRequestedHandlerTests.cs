using AutoFixture;

using FluentAssertions;

using MassTransit;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
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
    [Theory]
    [InlineData("EspnAwardsIndex.json", "https://sports.core.api.espn.com/v2/awards/index", DocumentType.Award)]
    [InlineData("EspnSeasonTypeWeeks.json", "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks?lang=en&region=us", DocumentType.SeasonTypeWeek)]
    public async Task WhenResourceIndexHasItems_EnqueuesEachItem(
        string fileName,
        string srcUrl,
        DocumentType documentType
        )
    {
        // arrange
        var json = await LoadJsonTestData(fileName); // valid EspnResourceIndexDto

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), false, true)).ReturnsAsync(json);

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, new Uri(srcUrl))
            .With(x => x.DocumentType, documentType)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        await handler.Consume(ctx);

        // assert
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task WhenResourceIndexHasNoItems_EnqueuesLeafDocument()
    {
        // arrange
        var json = "{\"items\": []}";

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), false, true)).ReturnsAsync(json);

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

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
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.Once);
    }

    [Fact]
    public async Task WhenDocumentIsNotIndex_TreatsAsLeaf()
    {
        // arrange
        var json = await LoadJsonTestData("EspnTeamBySeason.json");

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), false, true)).ReturnsAsync(json);

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

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
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.Once);
    }

    [Fact]
    public async Task WhenPagedIndex_AllPagesProcessed()
    {
        // arrange
        var page1 = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlays_Page0.json");
        var page2 = await LoadJsonTestData("EspnFootballNcaaEventCompetitionPlays_Page1.json");

        var uriPage1 = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628420/competitions/401628420/plays?lang=en&region=us&page=1&limit=25");
        var uriPage2 = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628420/competitions/401628420/plays?limit=25&page=2");

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();

        // Mock both cached and uncached fetches for both pages
        espnApi.Setup(x => x.GetResource(uriPage1, false, true)).ReturnsAsync(page1);
        espnApi.Setup(x => x.GetResource(uriPage1, true, true)).ReturnsAsync(page1);
        espnApi.Setup(x => x.GetResource(uriPage2, false, true)).ReturnsAsync(page2);
        espnApi.Setup(x => x.GetResource(uriPage2, true, true)).ReturnsAsync(page2);

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, uriPage1)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        await handler.Consume(ctx);

        // assert
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.AtLeast(50));
    }
    
    [Fact]
    public async Task WhenJsonInvalid_LogsAndReturns()
    {
        // arrange
        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), false, true)).ReturnsAsync("not json");

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, new Uri("https://sports.core.api.espn.com/v2/teams/invalid"))
            .With(x => x.DocumentType, DocumentType.TeamSeason)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        var ex = await Record.ExceptionAsync(() => handler.Consume(ctx));

        // assert
        ex.Should().BeNull();
    }
}
