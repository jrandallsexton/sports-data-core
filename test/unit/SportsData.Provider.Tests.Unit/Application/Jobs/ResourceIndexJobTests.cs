using AutoFixture;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Config;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System.Linq.Expressions;
using System.Text.Json;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Jobs;

public class ResourceIndexJobTests : ProviderTestBase<ResourceIndexJob>
{
    private const string SinglePageJson = "EspnResourceIndex_SinglePage.json";
    private const string MultiPageJson1 = "EspnResourceIndex_MultiPage_Page1.json";
    private const string MultiPageJson2 = "EspnResourceIndex_MultiPage_Page2.json";

    [Fact]
    public async Task When_ResourceIndexEntityNotFound_Should_LogError_AndExit()
    {
        // Arrange
        var def = Fixture.Create<DocumentJobDefinition>();

        var job = Mocker.CreateInstance<ResourceIndexJob>();

        // Act
        await job.ExecuteAsync(def);

        // Assert
        DataContext.ChangeTracker.Entries().Should().BeEmpty();
    }
    
    [Fact(Skip = "Hits real ESPN API.  Use sparingly.")]
    public async Task ExecuteAsync_Should_PageThrough_AllPages_FromRealEspnApi()
    {
        // Arrange
        var def = new DocumentJobDefinition
        {
            ResourceIndexId = Guid.NewGuid(),
            Sport =  Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            DocumentType = DocumentType.Franchise ,
            Endpoint = new Uri("http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/franchises"),
            EndpointMask = null,
            SeasonYear = null,
            StartPage = 1
        };

        var identityGenerator = new ExternalRefIdentityGenerator();
        var identity = identityGenerator.Generate(def.Endpoint.ToString());

        var resourceIndex = new ResourceIndex
        {
            Id = def.ResourceIndexId,
            Name = "NCAA Football Athletes",
            Uri = def.Endpoint,
            SourceUrlHash = identity.UrlHash,
            DocumentType = def.DocumentType,
            CronExpression = null,
            IsEnabled = true,
            IsRecurring = false,
            IsSeasonSpecific = false,
            Provider = def.SourceDataProvider,
            SportId = def.Sport,
            LastAccessedUtc = null,
            LastCompletedUtc = null,
            LastPageIndex = null,
            TotalPageCount = null,
            IsQueued = false
        };

        DataContext.ResourceIndexJobs.Add(resourceIndex);
        await DataContext.SaveChangesAsync();

        // Setup real ESPN client with default config (no cache, no persistence)
        var apiConfig = new EspnApiClientConfig
        {
            ReadFromCache = false,
            ForceLiveFetch = true,
            PersistLocally = false,
            LocalCacheDirectory = Path.Combine(Path.GetTempPath(), "espn-test-cache") // unused in this config
        };

        var httpClient = new HttpClient();
        var httpWrapper = new EspnHttpClient(httpClient, apiConfig, NullLogger<EspnHttpClient>.Instance);
        var realEspnApiClient = new EspnApiClient(httpWrapper, NullLogger<EspnApiClient>.Instance);

        // Inject the real client
        Mocker.Use<IProvideEspnApiData>(realEspnApiClient);

        // Force mismatch to ensure processing continues
        Mocker.GetMock<IDocumentStore>()
            .Setup(x => x.GetAllDocumentsAsync<DocumentBase>(It.IsAny<string>()))
            .ReturnsAsync(new List<DocumentBase>());

        var job = Mocker.CreateInstance<ResourceIndexJob>();

        // Act
        await job.ExecuteAsync(def);

        // Assert – can’t guarantee exact count, just verify something ran
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Verify(p =>
                    p.Enqueue(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()),
                Times.Exactly(788));
    }
}