#nullable enable

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

using FluentValidation.Results;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Documents;

public class DocumentRequestedHandlerTests : ProviderTestBase<DocumentRequestedHandler>
{
    [Theory]
    [InlineData("EspnAwardsIndex.json", "https://sports.core.api.espn.com/v2/awards/index", DocumentType.Award)]
    [InlineData("EspnSeasonTypeWeeks.json", "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/1/weeks?lang=en&region=us", DocumentType.SeasonTypeWeek)]
    [InlineData("EspnTeamSeasonRecords.json", "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/2/teams/99/record?lang=en", DocumentType.TeamSeasonRecord)]
    [InlineData("AthleteSeasonNotes.json", "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093/notes", DocumentType.AthleteSeasonNote)]
    public async Task WhenResourceIndexHasItems_EnqueuesEachItem(
        string fileName,
        string srcUrl,
        DocumentType documentType
        )
    {
        // arrange
        var json = await LoadJsonTestData(fileName); // valid EspnResourceIndexDto

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false)).ReturnsAsync(new Success<string>(json));

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
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false)).ReturnsAsync(new Success<string>(json));

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
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false)).ReturnsAsync(new Success<string>(json));

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

        // Mock to return page1 for first call (uriPage1), then page2 for any other URI
        var callCount = 0;
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false))
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? new Success<string>(page1) : new Success<string>(page2);
            });

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
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false)).ReturnsAsync(new Success<string>("not json"));

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
        var optionsMonitor = Mock.Of<IOptionsMonitor<EspnApiClientConfig>>(o => o.CurrentValue == apiConfig);
        var circuitBreaker = new Mock<IEspnCircuitBreaker>();
        var rateLimiter = new NoOpEspnRateLimiter();
        var httpWrapper = new EspnHttpClient(httpClient, optionsMonitor, NullLogger<EspnHttpClient>.Instance, circuitBreaker.Object, rateLimiter);
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
        var optionsMonitor = Mock.Of<IOptionsMonitor<EspnApiClientConfig>>(o => o.CurrentValue == apiConfig);
        var circuitBreaker = new Mock<IEspnCircuitBreaker>();
        var rateLimiter = new NoOpEspnRateLimiter();
        var httpWrapper = new EspnHttpClient(httpClient, optionsMonitor, NullLogger<EspnHttpClient>.Instance, circuitBreaker.Object, rateLimiter);
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
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false)).ReturnsAsync(new Success<string>(json));

        ProcessResourceIndexItemCommand? capturedCommand = null;
        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        background.Setup(x => x.Enqueue<IProcessResourceIndexItems>(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()))
            .Callback<Expression<Func<IProcessResourceIndexItems, Task>>>(expr =>
            {
                // Compile and invoke the expression to extract the command
                var func = expr.Compile();
                var mockProcessor = new Mock<IProcessResourceIndexItems>();
                mockProcessor.Setup(p => p.Process(It.IsAny<ProcessResourceIndexItemCommand>()))
                    .Callback<ProcessResourceIndexItemCommand>(cmd => capturedCommand = cmd)
                    .Returns(Task.CompletedTask);
                
                // Await the task returned by the compiled expression
                func(mockProcessor.Object).GetAwaiter().GetResult();
            });

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var baseUri = "http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2022/athletes/4686093/notes";
        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, new Uri(baseUri))
            .With(x => x.DocumentType, DocumentType.AthleteSeasonNote)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        await handler.Consume(ctx);

        // assert - verify that an item was enqueued even though the JSON items have no $ref
        background.Verify(x => x.Enqueue<IProcessResourceIndexItems>(
            It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), Times.Once);
        
        // assert - verify the constructed URI includes the filtered id parameter
        capturedCommand.Should().NotBeNull();
        capturedCommand!.Uri.ToString().Should().Contain("?id=171189");
        capturedCommand.Uri.ToString().Should().StartWith(baseUri);
    }

    [Fact]
    public async Task WhenHybridRefReturns404_UsesInlineJson()
    {
        // arrange
        var json = await LoadJsonTestData("EspnBrokenHybridPlays.json");

        var indexUri = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401029961/competitions/401029961/plays");
        var firstRefUri = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401029961/competitions/401029961/plays/4010299611");

        var espnApi = Mocker.GetMock<IProvideEspnApiData>();

        // Index fetch returns the hybrid JSON
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, false))
            .ReturnsAsync(new Success<string>(json));

        // Probe fetch returns 404 (matched by bypassCache=true, stripQuerystring=true which is the default)
        espnApi.Setup(x => x.GetResource(It.IsAny<Uri>(), true, true))
            .ReturnsAsync(new Failure<string>(default!, ResultStatus.NotFound,
                [new ValidationFailure("uri", "Not Found")]));

        var capturedCommands = new List<ProcessResourceIndexItemCommand>();
        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        background.Setup(x => x.Enqueue<IProcessResourceIndexItems>(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()))
            .Callback<Expression<Func<IProcessResourceIndexItems, Task>>>(expr =>
            {
                var func = expr.Compile();
                var mockProcessor = new Mock<IProcessResourceIndexItems>();
                mockProcessor.Setup(p => p.Process(It.IsAny<ProcessResourceIndexItemCommand>()))
                    .Callback<ProcessResourceIndexItemCommand>(cmd => capturedCommands.Add(cmd))
                    .Returns(Task.CompletedTask);
                func(mockProcessor.Object).GetAwaiter().GetResult();
            });

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, indexUri)
            .With(x => x.DocumentType, DocumentType.EventCompetitionPlay)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        await handler.Consume(ctx);

        // assert - all 3 items enqueued
        capturedCommands.Should().HaveCount(3);

        // assert - all commands have InlineJson populated
        capturedCommands.Should().AllSatisfy(cmd =>
        {
            cmd.InlineJson.Should().NotBeNullOrWhiteSpace();
            cmd.InlineJson.Should().Contain("\"id\"");
        });

        // assert - first command's inline JSON contains the correct play data
        capturedCommands[0].InlineJson.Should().Contain("Fumble Return Touchdown");
        capturedCommands[1].InlineJson.Should().Contain("Kickoff");
        capturedCommands[2].InlineJson.Should().Contain("Rush");
    }

    /// <summary>
    /// MLB EventCompetitionOdds is now classified as a leaf (sport-aware
    /// override in <see cref="EspnResourceIndexClassifier"/>) instead of a
    /// resource index. Items in MLB's odds wrapper lack both <c>$ref</c> and
    /// a top-level <c>id</c>, so the generic per-item extraction path in
    /// <see cref="DocumentRequestedHandler.ProcessResourceIndex"/> has nothing
    /// to work with. The leaf path enqueues the entire wrapper as a single
    /// document; a sport-specific Producer-side processor will iterate items
    /// and persist a CompetitionOdds row per provider.
    ///
    /// NCAAFB/NFL retain the original index behavior because their odds
    /// wrapper items each have a real `$ref` the generic path can follow —
    /// see the OddsEndpoint test in EspnResourceIndexClassifierTests.
    /// </summary>
    [Fact]
    public async Task WhenMlbEventCompetitionOdds_TreatsAsLeaf_EnqueuesSingleDocument()
    {
        // arrange — the JSON is loaded purely so any sanity check against the
        // mocked GetResource has realistic content; the leaf path doesn't fetch.
        await LoadJsonTestData("EspnBaseballMlbEventCompetitionOdds.json");

        var oddsListingUri = new Uri(
            "http://sports.core.api.espn.com/v2/sports/baseball/leagues/mlb/events/401814844/competitions/401814844/odds?lang=en&region=us");

        var capturedCommands = new List<ProcessResourceIndexItemCommand>();
        var background = Mocker.GetMock<IProvideBackgroundJobs>();
        background.Setup(x => x.Enqueue<IProcessResourceIndexItems>(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()))
            .Callback<Expression<Func<IProcessResourceIndexItems, Task>>>(expr =>
            {
                var func = expr.Compile();
                var mockProcessor = new Mock<IProcessResourceIndexItems>();
                mockProcessor.Setup(p => p.Process(It.IsAny<ProcessResourceIndexItemCommand>()))
                    .Callback<ProcessResourceIndexItemCommand>(cmd => capturedCommands.Add(cmd))
                    .Returns(Task.CompletedTask);
                func(mockProcessor.Object).GetAwaiter().GetResult();
            });

        var handler = Mocker.CreateInstance<DocumentRequestedHandler>();

        var msg = Fixture.Build<DocumentRequested>()
            .With(x => x.Uri, oddsListingUri)
            .With(x => x.DocumentType, DocumentType.EventCompetitionOdds)
            .With(x => x.SourceDataProvider, SourceDataProvider.Espn)
            .With(x => x.Sport, Sport.BaseballMlb)
            .OmitAutoProperties()
            .Create();

        var ctx = Mock.Of<ConsumeContext<DocumentRequested>>(x => x.Message == msg);

        // act
        await handler.Consume(ctx);

        // assert — exactly one ProcessResourceIndexItem command enqueued for
        // the listing URL itself (the leaf path doesn't fetch upstream)
        capturedCommands.Should().HaveCount(1);
        capturedCommands[0].Uri.Should().Be(oddsListingUri);
        capturedCommands[0].DocumentType.Should().Be(DocumentType.EventCompetitionOdds);
        capturedCommands[0].Sport.Should().Be(Sport.BaseballMlb);
        capturedCommands[0].InlineJson.Should().BeNull(
            "leaf path defers fetch+persist to ProcessResourceIndexItem; no inline data is attached at this stage");
    }
}
