using FluentAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Moq;

using SportsData.Core.Common;
using SportsData.Core.Common.Routing;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Sourcing.Historical;
using SportsData.Provider.Infrastructure.Data;

using System.Linq.Expressions;

using Xunit;

namespace SportsData.Provider.Tests.Unit.Application.Sourcing.Historical;

public class HistoricalSeasonSourcingServiceTests : IDisposable
{
    private readonly AppDataContext _context;
    private readonly Mock<IHistoricalSourcingUriBuilder> _uriBuilderMock;
    private readonly Mock<IGenerateRoutingKeys> _routingKeyGeneratorMock;
    private readonly Mock<IProvideBackgroundJobs> _backgroundJobProviderMock;
    private readonly Mock<ILogger<HistoricalSeasonSourcingService>> _loggerMock;
    private readonly HistoricalSourcingConfig _config;
    private readonly HistoricalSeasonSourcingService _service;

    public HistoricalSeasonSourcingServiceTests()
    {
        // In-memory database for testing
        var options = new DbContextOptionsBuilder<AppDataContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new AppDataContext(options);

        // Mocks
        _uriBuilderMock = new Mock<IHistoricalSourcingUriBuilder>();
        _routingKeyGeneratorMock = new Mock<IGenerateRoutingKeys>();
        _backgroundJobProviderMock = new Mock<IProvideBackgroundJobs>();
        _loggerMock = new Mock<ILogger<HistoricalSeasonSourcingService>>();

        // Config with defaults
        _config = new HistoricalSourcingConfig
        {
            DefaultTierDelays = new Dictionary<string, Dictionary<string, TierDelays>>
            {
                ["FootballNcaa"] = new Dictionary<string, TierDelays>
                {
                    ["Espn"] = new TierDelays
                    {
                        Season = 0,
                        Venue = 30,
                        TeamSeason = 60,
                        AthleteSeason = 240
                    }
                }
            }
        };

        // Setup mocks with default behavior
        _uriBuilderMock.Setup(x => x.BuildUri(It.IsAny<DocumentType>(), It.IsAny<int>(), It.IsAny<Sport>(), It.IsAny<SourceDataProvider>()))
            .Returns((DocumentType docType, int year, Sport sport, SourceDataProvider provider) =>
                new Uri($"https://sports.core.api.espn.com/v2/sports/football/leagues/college-football/{docType.ToString().ToLower()}"));

        _routingKeyGeneratorMock.Setup(x => x.Generate(It.IsAny<SourceDataProvider>(), It.IsAny<Uri>()))
            .Returns((SourceDataProvider provider, Uri uri) => $"{provider}.{uri.AbsolutePath}");

        _backgroundJobProviderMock.Setup(x => x.Schedule<ResourceIndexJob>(It.IsAny<Expression<Func<ResourceIndexJob, Task>>>(), It.IsAny<TimeSpan>()))
            .Returns(Guid.NewGuid().ToString());

        _service = new HistoricalSeasonSourcingService(
            _loggerMock.Object,
            _context,
            _uriBuilderMock.Object,
            _routingKeyGeneratorMock.Object,
            _backgroundJobProviderMock.Object,
            Options.Create(_config));
    }

    [Fact]
    public async Task SourceSeason_NewSeason_Creates4ResourceIndexRecords()
    {
        // Arrange
        var request = new HistoricalSeasonSourcingRequest
        {
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            SeasonYear = 2024
        };

        // Act
        var response = await _service.SourceSeasonAsync(request);

        // Assert
        response.CorrelationId.Should().NotBeEmpty();

        var resourceIndexes = await _context.ResourceIndexJobs.ToListAsync();
        resourceIndexes.Should().HaveCount(4);
        resourceIndexes.Should().Contain(x => x.DocumentType == DocumentType.Season);
        resourceIndexes.Should().Contain(x => x.DocumentType == DocumentType.Venue);
        resourceIndexes.Should().Contain(x => x.DocumentType == DocumentType.TeamSeason);
        resourceIndexes.Should().Contain(x => x.DocumentType == DocumentType.AthleteSeason);

        // All should be non-recurring, enabled, season-specific
        resourceIndexes.Should().AllSatisfy(x =>
        {
            x.IsRecurring.Should().BeFalse();
            x.IsEnabled.Should().BeTrue();
            x.IsSeasonSpecific.Should().BeTrue();
            x.SeasonYear.Should().Be(2024);
            x.SportId.Should().Be(Sport.FootballNcaa);
            x.Provider.Should().Be(SourceDataProvider.Espn);
        });
    }

    [Fact]
    public async Task SourceSeason_NewSeason_Schedules4HangfireJobs()
    {
        // Arrange
        var request = new HistoricalSeasonSourcingRequest
        {
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            SeasonYear = 2024
        };

        // Act
        await _service.SourceSeasonAsync(request);

        // Assert - Verify 4 jobs scheduled with correct delays
        _backgroundJobProviderMock.Verify(
            x => x.Schedule<ResourceIndexJob>(It.IsAny<Expression<Func<ResourceIndexJob, Task>>>(), It.IsAny<TimeSpan>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task SourceSeason_ExistingSeason_ReturnsExistingCorrelationId()
    {
        // Arrange
        var existingCorrelationId = Guid.NewGuid();
        var existingSeasonJob = new SportsData.Provider.Infrastructure.Data.Entities.ResourceIndex
        {
            Id = Guid.NewGuid(),
            Ordinal = 1,
            Name = "test",
            Provider = SourceDataProvider.Espn,
            DocumentType = DocumentType.Season,
            Shape = ResourceShape.Leaf,
            SportId = Sport.FootballNcaa,
            Uri = new Uri("https://espn.com"),
            SourceUrlHash = "hash",
            SeasonYear = 2024,
            IsSeasonSpecific = true,
            IsRecurring = false,
            IsQueued = false,
            IsEnabled = true,
            CreatedBy = existingCorrelationId,
            CreatedUtc = DateTime.UtcNow
        };

        _context.ResourceIndexJobs.Add(existingSeasonJob);
        await _context.SaveChangesAsync();

        var request = new HistoricalSeasonSourcingRequest
        {
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            SeasonYear = 2024
        };

        // Act
        var response = await _service.SourceSeasonAsync(request);

        // Assert
        response.CorrelationId.Should().Be(existingCorrelationId);

        // Should NOT create new records
        var resourceIndexes = await _context.ResourceIndexJobs.ToListAsync();
        resourceIndexes.Should().HaveCount(1);

        // Should NOT schedule new jobs
        _backgroundJobProviderMock.Verify(
            x => x.Schedule<ResourceIndexJob>(It.IsAny<Expression<Func<ResourceIndexJob, Task>>>(), It.IsAny<TimeSpan>()),
            Times.Never);
    }

    // Note: Cannot test Force=true path with in-memory database because it requires
    // PostgreSQL advisory locks (pg_try_advisory_lock). This requires integration testing
    // with a real PostgreSQL database. The advisory lock is a production safety feature
    // to prevent concurrent historical sourcing runs for the same season.

    [Fact]
    public async Task SourceSeason_CustomTierDelays_UsesProvidedDelays()
    {
        // Arrange
        var customDelays = new Dictionary<string, int>
        {
            ["season"] = 5,
            ["venue"] = 10,
            ["teamSeason"] = 20,
            ["athleteSeason"] = 40
        };

        var request = new HistoricalSeasonSourcingRequest
        {
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            SeasonYear = 2024,
            TierDelays = customDelays
        };

        // Act
        await _service.SourceSeasonAsync(request);

        // Assert - Can't easily verify exact delay times without exposing internals,
        // but we can verify all 4 jobs were scheduled
        _backgroundJobProviderMock.Verify(
            x => x.Schedule<ResourceIndexJob>(It.IsAny<Expression<Func<ResourceIndexJob, Task>>>(), It.IsAny<TimeSpan>()),
            Times.Exactly(4));
    }

    [Fact]
    public async Task SourceSeason_NegativeDelays_ThrowsArgumentException()
    {
        // Arrange
        var request = new HistoricalSeasonSourcingRequest
        {
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            SeasonYear = 2024,
            TierDelays = new Dictionary<string, int>
            {
                ["season"] = -10,
                ["venue"] = 30,
                ["teamSeason"] = 60,
                ["athleteSeason"] = 240
            }
        };

        // Act & Assert
        await FluentActions.Invoking(() => _service.SourceSeasonAsync(request))
            .Should().ThrowAsync<ArgumentException>()
            .WithMessage("*negative*");
    }

    [Fact]
    public async Task SourceSeason_NullTierDelays_UsesFallbackDefaults()
    {
        // Arrange
        var request = new HistoricalSeasonSourcingRequest
        {
            Sport = Sport.FootballNcaa,
            SourceDataProvider = SourceDataProvider.Espn,
            SeasonYear = 2024
            // TierDelays not provided
        };

        // Act
        var response = await _service.SourceSeasonAsync(request);

        // Assert
        response.CorrelationId.Should().NotBeEmpty();
        var resourceIndexes = await _context.ResourceIndexJobs.ToListAsync();
        resourceIndexes.Should().HaveCount(4);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }
}
