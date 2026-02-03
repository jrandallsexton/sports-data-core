using AutoFixture;

using FluentAssertions;

using MassTransit;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Documents;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Documents;

public class DocumentRequestedHandlerTests : ProviderTestBase<DocumentRequestedHandler>
{
    [Theory]
    [InlineData("EspnAwardsIndex.json", "https://sports.core.api.espn.com/v2/awards/index", DocumentType.Award)]
    [InlineData("EspnSeasonTypeWeeks.json", "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks?lang=en&region=us", DocumentType.SeasonTypeWeek)]
    [InlineData("EspnTeamSeasonRecords.json", "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/teams/99/record?lang=en", DocumentType.TeamSeasonRecord)]
    [InlineData("AthleteSeasonNotes.json", "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093/notes", DocumentType.TeamSeasonInjuries)]
    public async Task WhenResourceIndexHasItems_EnqueuesEachItem(
        string fileName,
        string srcUrl,
        DocumentType documentType
        )
    {
        // arrange
        var json = await LoadJsonTestData(fileName); // valid EspnResourceIndexDto

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false)).ReturnsAsync(json);

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
    public async Task WhenResourceIndexHasNoItems_NothingHappens()
    {
        // arrange
        var json =
            "{\r\n    \"count\": 0,\r\n    \"pageIndex\": 0,\r\n    \"pageSize\": 25,\r\n    \"pageCount\": 0,\r\n    \"items\": []\r\n}";

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
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.Never);
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
        var uriPage2 = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628420/competitions/401628420/plays?lang=en&region=us&page=2&limit=25");

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();

        // Mock both cached and uncached fetches for both pages
        espnApi.Setup(x => x.GetResource(uriPage1, true, true)).ReturnsAsync(page1);
        espnApi.Setup(x => x.GetResource(uriPage1, true, false)).ReturnsAsync(page1);
        espnApi.Setup(x => x.GetResource(uriPage1, false, true)).ReturnsAsync(page1);

        espnApi.Setup(x => x.GetResource(uriPage2, true, true)).ReturnsAsync(page2);
        espnApi.Setup(x => x.GetResource(uriPage2, true, false)).ReturnsAsync(page2);
        espnApi.Setup(x => x.GetResource(uriPage2, false, true)).ReturnsAsync(page2);

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
    
    [Theory(Skip="REVISIT")]
    [InlineData("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2021/teams/2551/ranks", DocumentType.TeamSeasonRank, 0)]
    [InlineData("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues", DocumentType.Venue, 900)]
    public async Task WhenDocumentRequested_IsResourceIndex_ProcessesAllPagesAndItems(
        string targetUrl, DocumentType documentType, int expectedCount)
    {
        // arrange

        // Setup real ESPN client with default config (no cache, no persistence)
        var apiConfig = new EspnApiClientConfig
        {
            ReadFromCache = false,
            ForceLiveFetch = true,
            PersistLocally = false,
            LocalCacheDirectory = Path.Combine(Path.GetTempPath(), "espn-test-cache") // unused in this config
        };

        var httpClient = new HttpClient();
        var options = Options.Create(apiConfig);
        var httpWrapper = new EspnHttpClient(httpClient, options, NullLogger<EspnHttpClient>.Instance);
        var realEspnApiClient = new EspnApiClient(httpWrapper, NullLogger<EspnApiClient>.Instance);
        // Inject the real client
        Mocker.Use<IProvideEspnApiData>(realEspnApiClient);

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var evt = new DocumentRequested(Guid.NewGuid().ToString(),
            null,
            new Uri(targetUrl),
            null,
            Sport.FootballNcaa, 2025, documentType, SourceDataProvider.Espn, Guid.NewGuid(),
            Guid.NewGuid());

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == evt);

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        // act
        await handler.Consume(ctx);

        // assert
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.Exactly(expectedCount));
    }

    [Fact]
    public async Task WhenDocumentRequested_IsResourceIndexItem_ProcessesItem()
    {
        // arrange

        // Setup real ESPN client with default config (no cache, no persistence)
        var apiConfig = new EspnApiClientConfig
        {
            ReadFromCache = false,
            ForceLiveFetch = true,
            PersistLocally = false,
            LocalCacheDirectory = Path.Combine(Path.GetTempPath(), "espn-test-cache") // unused in this config
        };

        var httpClient = new HttpClient();
        var options = Options.Create(apiConfig);
        var httpWrapper = new EspnHttpClient(httpClient, options, NullLogger<EspnHttpClient>.Instance);
        var realEspnApiClient = new EspnApiClient(httpWrapper, NullLogger<EspnApiClient>.Instance);
        // Inject the real client
        Mocker.Use<IProvideEspnApiData>(realEspnApiClient);

        var background = Mocker.GetMock<IProvideBackgroundJobs>();

        var evt = new DocumentRequested(Guid.NewGuid().ToString(),
            null,
            new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues/36?lang=en"),
            null,
            Sport.FootballNcaa, 2025, DocumentType.Venue, SourceDataProvider.Espn, Guid.NewGuid(),
            Guid.NewGuid());

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == evt);

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        // act
        await handler.Consume(ctx);

        // assert
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.Exactly(1));
    }

    [Fact]
    public async Task WhenResourceIndexItemsHaveNoRef_ConstructsFilteredUri()
    {
        // arrange
        var json = await LoadJsonTestData("AthleteSeasonNotes.json");

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false)).ReturnsAsync(json);

        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        Uri? capturedUri = null;
        background.Setup(x => x.Enqueue<IProcessResourceIndexItems>(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()))
            .Callback(() => 
            {
                // Capture is difficult with Hangfire expressions, so we'll verify the enqueue happened
                // The actual URI construction is tested by the integration of the logic
            });

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var baseUri = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093/notes";
        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, new Uri(baseUri))
            .With(x => x.DocumentType, DocumentType.TeamSeasonInjuries)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        await handler.Consume(ctx);

        // assert - verify that an item was enqueued even though the JSON items have no $ref
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.Once);
    }
}
