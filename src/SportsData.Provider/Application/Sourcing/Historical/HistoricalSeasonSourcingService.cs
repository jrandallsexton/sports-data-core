using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Common.Routing;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Infrastructure.Data;
using ResourceIndexEntity = SportsData.Provider.Infrastructure.Data.Entities.ResourceIndex;

namespace SportsData.Provider.Application.Sourcing.Historical;

public interface IHistoricalSeasonSourcingService
{
    Task<HistoricalSeasonSourcingResponse> SourceSeasonAsync(
        HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken = default);
}

public class HistoricalSeasonSourcingService : IHistoricalSeasonSourcingService
{
    private readonly ILogger<HistoricalSeasonSourcingService> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IHistoricalSourcingUriBuilder _uriBuilder;
    private readonly IGenerateRoutingKeys _routingKeyGenerator;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly HistoricalSourcingConfig _config;

    public HistoricalSeasonSourcingService(
        ILogger<HistoricalSeasonSourcingService> logger,
        AppDataContext dataContext,
        IHistoricalSourcingUriBuilder uriBuilder,
        IGenerateRoutingKeys routingKeyGenerator,
        IProvideBackgroundJobs backgroundJobProvider,
        IOptions<HistoricalSourcingConfig> config)
    {
        _logger = logger;
        _dataContext = dataContext;
        _uriBuilder = uriBuilder;
        _routingKeyGenerator = routingKeyGenerator;
        _backgroundJobProvider = backgroundJobProvider;
        _config = config.Value;
    }

    public async Task<HistoricalSeasonSourcingResponse> SourceSeasonAsync(
        HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Sport"] = request.Sport,
            ["Provider"] = request.SourceDataProvider,
            ["SeasonYear"] = request.SeasonYear
        }))
        {
            _logger.LogInformation(
                "Starting historical season sourcing. Sport={Sport}, Provider={Provider}, Year={Year}",
                request.Sport, request.SourceDataProvider, request.SeasonYear);

            // Get tier delays (from request or config defaults)
            var tierDelays = GetTierDelays(request);

            // Define the tiers to process
            var tiers = new[]
            {
                new TierDefinition(DocumentType.Season, ResourceShape.Leaf, tierDelays.Season),
                new TierDefinition(DocumentType.Venue, ResourceShape.Index, tierDelays.Venue),
                new TierDefinition(DocumentType.TeamSeason, ResourceShape.Index, tierDelays.TeamSeason),
                new TierDefinition(DocumentType.AthleteSeason, ResourceShape.Index, tierDelays.AthleteSeason)
            };

            var startTime = DateTime.UtcNow;
            
            // Use timestamp-based ordinals to avoid race conditions with concurrent requests
            // Format: YYYYMMDDHHmmss (14 digits) + tier index (2 digits)
            // Example: 20251214103045 + 00 = 2025121410304500
            var baseOrdinal = long.Parse(startTime.ToString("yyyyMMddHHmmss"));

            // Create ResourceIndex records (collect them for scheduling after persistence)
            var createdResourceIndexes = new List<(ResourceIndexEntity Entity, TimeSpan Delay)>();

            for (var i = 0; i < tiers.Length; i++)
            {
                var tier = tiers[i];
                var uri = _uriBuilder.BuildUri(tier.DocumentType, request.SeasonYear, request.Sport, request.SourceDataProvider);

                var resourceIndex = new ResourceIndexEntity
                {
                    Id = Guid.NewGuid(),
                    Ordinal = baseOrdinal * 100L + i, // Timestamp + tier index ensures uniqueness (use long to avoid overflow)
                    Name = _routingKeyGenerator.Generate(request.SourceDataProvider, uri),
                    IsRecurring = false,
                    IsQueued = false,
                    CronExpression = null,
                    IsEnabled = true,
                    Provider = request.SourceDataProvider,
                    DocumentType = tier.DocumentType,
                    Shape = tier.Shape,
                    SportId = request.Sport,
                    Uri = uri,
                    SourceUrlHash = HashProvider.GenerateHashFromUri(uri),
                    SeasonYear = request.SeasonYear,
                    IsSeasonSpecific = true,
                    LastAccessedUtc = null,
                    LastCompletedUtc = null,
                    LastPageIndex = null,
                    TotalPageCount = null,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = correlationId // Use correlationId to track which historical sourcing job created this
                };

                await _dataContext.ResourceIndexJobs.AddAsync(resourceIndex, cancellationToken);

                // Store for scheduling after persistence
                var delayTimeSpan = TimeSpan.FromMinutes(tier.DelayMinutes);
                createdResourceIndexes.Add((resourceIndex, delayTimeSpan));

                _logger.LogInformation(
                    "Created ResourceIndex for tier. Tier={TierName}, DocumentType={DocumentType}, Delay={DelayMinutes}min, " +
                    "Ordinal={Ordinal}, ResourceIndexId={ResourceIndexId}, Uri={Uri}",
                    tier.DocumentType, tier.DocumentType, tier.DelayMinutes,
                    resourceIndex.Ordinal, resourceIndex.Id, uri);
            }

            // Persist all ResourceIndex records BEFORE scheduling jobs
            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("ResourceIndex records persisted. Scheduling {Count} jobs...", createdResourceIndexes.Count);

            // Now schedule jobs against persisted records
            foreach (var (resourceIndex, delay) in createdResourceIndexes)
            {
                var jobDefinition = new DocumentJobDefinition(resourceIndex);

                _backgroundJobProvider.Schedule<ResourceIndexJob>(
                    job => job.ExecuteAsync(jobDefinition),
                    delay);

                _logger.LogInformation(
                    "Scheduled job for tier. DocumentType={DocumentType}, ResourceIndexId={ResourceIndexId}, Delay={Delay}",
                    resourceIndex.DocumentType, resourceIndex.Id, delay);
            }

            _logger.LogInformation(
                "Historical season sourcing initiated. {TierCount} tiers scheduled. CorrelationId={CorrelationId}",
                tiers.Length, correlationId);

            return new HistoricalSeasonSourcingResponse
            {
                CorrelationId = correlationId
            };
        }
    }

    private TierDelays GetTierDelays(HistoricalSeasonSourcingRequest request)
    {
        // Try to get from request first
        if (request.TierDelays != null && request.TierDelays.Count > 0)
        {
            return new TierDelays
            {
                Season = request.TierDelays.GetValueOrDefault("season", 0),
                Venue = request.TierDelays.GetValueOrDefault("venue", 30),
                TeamSeason = request.TierDelays.GetValueOrDefault("teamSeason", 60),
                AthleteSeason = request.TierDelays.GetValueOrDefault("athleteSeason", 240)
            };
        }

        // Fall back to config defaults
        var sportKey = request.Sport.ToString();
        var providerKey = request.SourceDataProvider.ToString();

        if (_config.DefaultTierDelays.TryGetValue(sportKey, out var sportDelays) &&
            sportDelays.TryGetValue(providerKey, out var delays))
        {
            return delays;
        }

        // Ultimate fallback (should not happen if config is correct)
        _logger.LogWarning(
            "No tier delays configured for {Sport}/{Provider}, using hardcoded defaults",
            request.Sport, request.SourceDataProvider);

        return new TierDelays
        {
            Season = 0,
            Venue = 30,
            TeamSeason = 60,
            AthleteSeason = 240
        };
    }

    private record TierDefinition(DocumentType DocumentType, ResourceShape Shape, int DelayMinutes);
}
