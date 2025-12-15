using AutoFixture;

using FluentAssertions;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Application.Processors;
using SportsData.Provider.Infrastructure.Data;
using SportsData.Provider.Infrastructure.Data.Entities;
using SportsData.Provider.Infrastructure.Providers.Espn;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Jobs;

public class ResourceIndexJobTests : ProviderTestBase<ResourceIndexJob>
{
    private const string SinglePageJson = "EspnResourceIndex_SinglePage.json";
    private const string MultiPageJson1 = "EspnResourceIndex_MultiPage_Page1.json";
    private const string MultiPageJson2 = "EspnResourceIndex_MultiPage_Page2.json";

    [Fact(Skip = "methods 'ExecuteUpdate' and 'ExecuteUpdateAsync' are not supported by the current database provider.")]
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
        var options = Options.Create(apiConfig);
        var httpWrapper = new EspnHttpClient(httpClient, options, NullLogger<EspnHttpClient>.Instance);
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

        // Assert – can't guarantee exact count, just verify something ran
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Verify(p =>
                    p.Enqueue(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()),
                Times.Exactly(788));
    }

    [Fact(Skip = "ExecuteUpdate not supported by InMemory provider")]
    public async Task When_UpstreamTier_FailedToComplete_Should_CancelDownstreamTier()
    {
        // Arrange - Simulate a historical sourcing run where Season tier failed
        var correlationId = Guid.NewGuid();
        var seasonYear = 2024;
        
        // Create Season tier that started but failed (has ProcessingStartedUtc but no LastCompletedUtc)
        var seasonResourceIndex = new ResourceIndex
        {
            Id = Guid.NewGuid(),
            Name = "Season-2024",
            Uri = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024"),
            SourceUrlHash = "season-hash",
            DocumentType = DocumentType.Season,
            Shape = ResourceShape.Leaf,
            CronExpression = null,
            IsEnabled = true,
            IsRecurring = false,
            IsSeasonSpecific = true,
            Provider = SourceDataProvider.Espn,
            SportId = Sport.FootballNcaa,
            SeasonYear = seasonYear,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStartedUtc = DateTime.UtcNow.AddMinutes(-5), // Started 5 minutes ago
            LastCompletedUtc = null, // ❌ Never completed - FAILED
            LastAccessedUtc = null,
            LastPageIndex = null,
            TotalPageCount = null,
            IsQueued = false
        };

        // Create Venue tier (depends on Season)
        var venueResourceIndexId = Guid.NewGuid();
        var venueResourceIndex = new ResourceIndex
        {
            Id = venueResourceIndexId,
            Name = "Venues-2024",
            Uri = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues"),
            SourceUrlHash = "venue-hash",
            DocumentType = DocumentType.Venue,
            Shape = ResourceShape.Index,
            CronExpression = null,
            IsEnabled = true,
            IsRecurring = false,
            IsSeasonSpecific = true,
            Provider = SourceDataProvider.Espn,
            SportId = Sport.FootballNcaa,
            SeasonYear = seasonYear,
            CreatedBy = correlationId, // Same historical sourcing run
            CreatedUtc = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStartedUtc = null,
            LastCompletedUtc = null,
            LastAccessedUtc = null,
            LastPageIndex = null,
            TotalPageCount = null,
            IsQueued = false,
            ProcessingInstanceId = null
        };

        DataContext.ResourceIndexJobs.Add(seasonResourceIndex);
        DataContext.ResourceIndexJobs.Add(venueResourceIndex);
        await DataContext.SaveChangesAsync();

        var venueDef = new DocumentJobDefinition
        {
            ResourceIndexId = venueResourceIndexId,
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            DocumentType = DocumentType.Venue,
            Endpoint = venueResourceIndex.Uri,
            EndpointMask = null,
            SeasonYear = seasonYear,
            Shape = ResourceShape.Index
        };

        var job = Mocker.CreateInstance<ResourceIndexJob>();

        // Act
        await job.ExecuteAsync(venueDef);

        // Assert
        var updatedVenueJob = await DataContext.ResourceIndexJobs.FindAsync(venueResourceIndexId);
        updatedVenueJob.Should().NotBeNull();
        updatedVenueJob!.IsEnabled.Should().BeFalse("Venue tier should be disabled due to Season failure");
        updatedVenueJob.IsQueued.Should().BeFalse("Venue tier should not be queued");
        updatedVenueJob.LastCompletedUtc.Should().BeNull("Venue tier should not have completed");

        // Verify no items were enqueued for processing
        Mocker.GetMock<IProvideBackgroundJobs>()
            .Verify(p => p.Enqueue(It.IsAny<Expression<Func<IProcessResourceIndexItems, Task>>>()), 
                Times.Never, 
                "No items should be enqueued when tier is cancelled");
    }

    [Fact(Skip = "ExecuteUpdate not supported by InMemory provider")]
    public async Task When_UpstreamTier_IsStillProcessing_Should_ThrowException_ToRetryLater()
    {
        // Arrange - Simulate Season tier currently processing
        var correlationId = Guid.NewGuid();
        var seasonYear = 2024;
        
        // Create Season tier that is currently processing (has ProcessingInstanceId)
        var seasonResourceIndex = new ResourceIndex
        {
            Id = Guid.NewGuid(),
            Name = "Season-2024",
            Uri = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024"),
            SourceUrlHash = "season-hash",
            DocumentType = DocumentType.Season,
            Shape = ResourceShape.Leaf,
            CronExpression = null,
            IsEnabled = true,
            IsRecurring = false,
            IsSeasonSpecific = true,
            Provider = SourceDataProvider.Espn,
            SportId = Sport.FootballNcaa,
            SeasonYear = seasonYear,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStartedUtc = DateTime.UtcNow.AddMinutes(-2), // Started 2 minutes ago
            ProcessingInstanceId = Guid.NewGuid(), // 🔄 Currently processing
            LastCompletedUtc = null,
            LastAccessedUtc = DateTime.UtcNow.AddMinutes(-2),
            LastPageIndex = null,
            TotalPageCount = null,
            IsQueued = true
        };

        // Create Venue tier (depends on Season)
        var venueResourceIndexId = Guid.NewGuid();
        var venueResourceIndex = new ResourceIndex
        {
            Id = venueResourceIndexId,
            Name = "Venues-2024",
            Uri = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues"),
            SourceUrlHash = "venue-hash",
            DocumentType = DocumentType.Venue,
            Shape = ResourceShape.Index,
            CronExpression = null,
            IsEnabled = true,
            IsRecurring = false,
            IsSeasonSpecific = true,
            Provider = SourceDataProvider.Espn,
            SportId = Sport.FootballNcaa,
            SeasonYear = seasonYear,
            CreatedBy = correlationId, // Same historical sourcing run
            CreatedUtc = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStartedUtc = null,
            LastCompletedUtc = null,
            LastAccessedUtc = null,
            LastPageIndex = null,
            TotalPageCount = null,
            IsQueued = false,
            ProcessingInstanceId = null
        };

        DataContext.ResourceIndexJobs.Add(seasonResourceIndex);
        DataContext.ResourceIndexJobs.Add(venueResourceIndex);
        await DataContext.SaveChangesAsync();

        var venueDef = new DocumentJobDefinition
        {
            ResourceIndexId = venueResourceIndexId,
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            DocumentType = DocumentType.Venue,
            Endpoint = venueResourceIndex.Uri,
            EndpointMask = null,
            SeasonYear = seasonYear,
            Shape = ResourceShape.Index
        };

        var job = Mocker.CreateInstance<ResourceIndexJob>();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await job.ExecuteAsync(venueDef));

        exception.Message.Should().Contain("still processing", 
            "Exception message should indicate upstream is still processing");
        exception.Message.Should().Contain("Season", 
            "Exception should mention the Season tier");

        // Verify job was not disabled (should retry later)
        var updatedVenueJob = await DataContext.ResourceIndexJobs.FindAsync(venueResourceIndexId);
        updatedVenueJob.Should().NotBeNull();
        updatedVenueJob!.IsEnabled.Should().BeTrue("Venue tier should remain enabled for retry");
    }

    [Fact(Skip = "ExecuteUpdate not supported by InMemory provider")]
    public async Task When_AllUpstreamTiers_Completed_Should_ProceedWithProcessing()
    {
        // Arrange - Simulate successful Season tier completion
        var correlationId = Guid.NewGuid();
        var seasonYear = 2024;
        
        // Create Season tier that completed successfully
        var seasonResourceIndex = new ResourceIndex
        {
            Id = Guid.NewGuid(),
            Name = "Season-2024",
            Uri = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2024"),
            SourceUrlHash = "season-hash",
            DocumentType = DocumentType.Season,
            Shape = ResourceShape.Leaf,
            CronExpression = null,
            IsEnabled = true,
            IsRecurring = false,
            IsSeasonSpecific = true,
            Provider = SourceDataProvider.Espn,
            SportId = Sport.FootballNcaa,
            SeasonYear = seasonYear,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStartedUtc = DateTime.UtcNow.AddMinutes(-5),
            LastCompletedUtc = DateTime.UtcNow.AddMinutes(-2), // ✅ Completed successfully
            LastAccessedUtc = DateTime.UtcNow.AddMinutes(-2),
            LastPageIndex = 1,
            TotalPageCount = 1,
            IsQueued = false,
            ProcessingInstanceId = null
        };

        // Create Venue tier (depends on Season)
        var venueResourceIndexId = Guid.NewGuid();
        var venueResourceIndex = new ResourceIndex
        {
            Id = venueResourceIndexId,
            Name = "Venues-2024",
            Uri = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues"),
            SourceUrlHash = "venue-hash",
            DocumentType = DocumentType.Venue,
            Shape = ResourceShape.Index,
            CronExpression = null,
            IsEnabled = true,
            IsRecurring = false,
            IsSeasonSpecific = true,
            Provider = SourceDataProvider.Espn,
            SportId = Sport.FootballNcaa,
            SeasonYear = seasonYear,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow.AddMinutes(-10),
            ProcessingStartedUtc = null,
            LastCompletedUtc = null,
            LastAccessedUtc = null,
            LastPageIndex = null,
            TotalPageCount = null,
            IsQueued = false,
            ProcessingInstanceId = null
        };

        DataContext.ResourceIndexJobs.Add(seasonResourceIndex);
        DataContext.ResourceIndexJobs.Add(venueResourceIndex);
        await DataContext.SaveChangesAsync();

        // Mock ESPN API to return empty index
        var emptyIndex = new EspnResourceIndexDto
        {
            Count = 0,
            PageCount = 1,
            PageIndex = 1,
            Items = new List<EspnResourceIndexItem>()
        };

        Mocker.GetMock<IProvideEspnApiData>()
            .Setup(x => x.GetResourceIndex(It.IsAny<Uri>(), It.IsAny<string>()))
            .ReturnsAsync(emptyIndex);

        var venueDef = new DocumentJobDefinition
        {
            ResourceIndexId = venueResourceIndexId,
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            DocumentType = DocumentType.Venue,
            Endpoint = venueResourceIndex.Uri,
            EndpointMask = null,
            SeasonYear = seasonYear,
            Shape = ResourceShape.Index
        };

        var job = Mocker.CreateInstance<ResourceIndexJob>();

        // Act
        await job.ExecuteAsync(venueDef);

        // Assert
        var updatedVenueJob = await DataContext.ResourceIndexJobs.FindAsync(venueResourceIndexId);
        updatedVenueJob.Should().NotBeNull();
        updatedVenueJob!.IsEnabled.Should().BeTrue("Venue tier should remain enabled");
        updatedVenueJob.LastCompletedUtc.Should().NotBeNull("Venue tier should have completed");
        updatedVenueJob.IsQueued.Should().BeFalse("Venue tier should not be queued after completion");

        // Verify processing happened (even though index was empty)
        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(x => x.GetResourceIndex(It.IsAny<Uri>(), It.IsAny<string>()), 
                Times.Once, 
                "ESPN API should have been called when upstream completed successfully");
    }

    [Fact(Skip = "ExecuteUpdate not supported by InMemory provider")]
    public async Task When_RecurringJob_Should_SkipUpstreamValidation()
    {
        // Arrange - Regular recurring job (not historical sourcing)
        var resourceIndexId = Guid.NewGuid();
        
        // Create recurring Venue job (CreatedBy = Guid.Empty, not part of historical sourcing)
        var venueResourceIndex = new ResourceIndex
        {
            Id = resourceIndexId,
            Name = "Venues-Recurring",
            Uri = new Uri("https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/venues"),
            SourceUrlHash = "venue-hash",
            DocumentType = DocumentType.Venue,
            Shape = ResourceShape.Index,
            CronExpression = "0 */6 * * *", // Every 6 hours
            IsEnabled = true,
            IsRecurring = true, // ✅ Recurring job
            IsSeasonSpecific = false,
            Provider = SourceDataProvider.Espn,
            SportId = Sport.FootballNcaa,
            SeasonYear = null,
            CreatedBy = Guid.Empty, // Not part of historical sourcing
            CreatedUtc = DateTime.UtcNow.AddDays(-30),
            ProcessingStartedUtc = null,
            LastCompletedUtc = DateTime.UtcNow.AddHours(-6),
            LastAccessedUtc = null,
            LastPageIndex = null,
            TotalPageCount = null,
            IsQueued = false,
            ProcessingInstanceId = null
        };

        DataContext.ResourceIndexJobs.Add(venueResourceIndex);
        await DataContext.SaveChangesAsync();

        // Mock ESPN API
        var emptyIndex = new EspnResourceIndexDto
        {
            Count = 0,
            PageCount = 1,
            PageIndex = 1,
            Items = new List<EspnResourceIndexItem>()
        };

        Mocker.GetMock<IProvideEspnApiData>()
            .Setup(x => x.GetResourceIndex(It.IsAny<Uri>(), It.IsAny<string>()))
            .ReturnsAsync(emptyIndex);

        var def = new DocumentJobDefinition
        {
            ResourceIndexId = resourceIndexId,
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            DocumentType = DocumentType.Venue,
            Endpoint = venueResourceIndex.Uri,
            EndpointMask = null,
            SeasonYear = null,
            Shape = ResourceShape.Index
        };

        var job = Mocker.CreateInstance<ResourceIndexJob>();

        // Act
        await job.ExecuteAsync(def);

        // Assert - should process without checking upstream (because it's recurring, not historical)
        var updatedJob = await DataContext.ResourceIndexJobs.FindAsync(resourceIndexId);
        updatedJob.Should().NotBeNull();
        updatedJob!.LastCompletedUtc.Should().NotBeNull("Recurring job should complete normally");
        updatedJob.IsEnabled.Should().BeTrue("Recurring job should remain enabled");
        
        // Verify API was called (no upstream validation blocked it)
        Mocker.GetMock<IProvideEspnApiData>()
            .Verify(x => x.GetResourceIndex(It.IsAny<Uri>(), It.IsAny<string>()), 
                Times.Once, 
                "Recurring job should proceed without upstream validation");
    }
}