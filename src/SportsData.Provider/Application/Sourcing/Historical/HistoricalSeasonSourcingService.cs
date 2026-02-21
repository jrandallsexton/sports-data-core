using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Common.Routing;
using SportsData.Core.Processing;
using SportsData.Provider.Application.Jobs;
using SportsData.Provider.Application.Jobs.Definitions;
using SportsData.Provider.Infrastructure.Data;

using System.Security.Cryptography;
using System.Text;

using ResourceIndexEntity = SportsData.Provider.Infrastructure.Data.Entities.ResourceIndex;

namespace SportsData.Provider.Application.Sourcing.Historical;

public interface IHistoricalSeasonSourcingService
{
    Task<HistoricalSeasonSourcingResponse> SourceSeasonAsync(
        HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken = default);
    
    Task<Guid> CreateSagaResourceIndexesAsync(
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

            var tierDelays = GetTierDelays(request);
            ValidateTierDelays(tierDelays);

            var existingSeasonJob = await FindExistingSeasonJobAsync(request, cancellationToken);

            if (existingSeasonJob != null)
            {
                return await HandleExistingSeasonAsync(request, existingSeasonJob, tierDelays, correlationId, cancellationToken);
            }

            return await CreateNewSeasonSourcingAsync(request, tierDelays, correlationId, cancellationToken);
        }
    }

    /// <summary>
    /// Finds an existing ResourceIndex job for the specified season, if any.
    /// </summary>
    private async Task<ResourceIndexEntity?> FindExistingSeasonJobAsync(
        HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken)
    {
        return await _dataContext.ResourceIndexJobs
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.Provider == request.SourceDataProvider &&
                x.SportId == request.Sport &&
                x.DocumentType == DocumentType.Season &&
                x.SeasonYear == request.SeasonYear,
                cancellationToken);
    }

    /// <summary>
    /// Validates that all tier delays are non-negative.
    /// </summary>
    private static void ValidateTierDelays(TierDelays tierDelays)
    {
        if (tierDelays.Season < 0 || tierDelays.Venue < 0 || tierDelays.TeamSeason < 0 || tierDelays.AthleteSeason < 0)
        {
            throw new ArgumentException("Tier delays cannot be negative.");
        }
    }

    /// <summary>
    /// Handles a request for a season that already has ResourceIndex jobs created.
    /// Returns idempotent response or force-reschedules if requested.
    /// </summary>
    private async Task<HistoricalSeasonSourcingResponse> HandleExistingSeasonAsync(
        HistoricalSeasonSourcingRequest request,
        ResourceIndexEntity existingSeasonJob,
        TierDelays tierDelays,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (request.Force)
        {
            return await ForceRescheduleSeasonAsync(request, existingSeasonJob, tierDelays, correlationId, cancellationToken);
        }

        _logger.LogWarning(
            "Historical sourcing for {Year} already exists. ResourceIndexId={Id}, CreatedUtc={CreatedUtc}",
            request.SeasonYear, existingSeasonJob.Id, existingSeasonJob.CreatedUtc);

        return new HistoricalSeasonSourcingResponse
        {
            CorrelationId = existingSeasonJob.CreatedBy
        };
    }

    /// <summary>
    /// Force-reschedules all tier jobs for an existing season.
    /// Uses PostgreSQL advisory lock to prevent concurrent force requests.
    /// </summary>
    private async Task<HistoricalSeasonSourcingResponse> ForceRescheduleSeasonAsync(
        HistoricalSeasonSourcingRequest request,
        ResourceIndexEntity existingSeasonJob,
        TierDelays tierDelays,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var lockId = GetAdvisoryLockId(request.SourceDataProvider, request.Sport, request.SeasonYear);
        var lockAcquired = false;

        try
        {
            lockAcquired = await TryAcquireAdvisoryLockAsync(lockId, cancellationToken);

            if (!lockAcquired)
            {
                return HandleConcurrentForceRequest(request, correlationId, lockId);
            }

            _logger.LogInformation(
                "Force reschedule lock acquired. Sport={Sport}, Provider={Provider}, Year={Year}, LockId={LockId}",
                request.Sport, request.SourceDataProvider, request.SeasonYear, lockId);

            var existingJobs = await FetchExistingTierJobsAsync(request, cancellationToken);
            var (scheduledCount, failedCount) = RescheduleTierJobs(existingJobs, tierDelays);

            _logger.LogInformation(
                "Force reschedule completed. ScheduledJobs={ScheduledCount}, FailedJobs={FailedCount}, TotalJobs={TotalJobs}",
                scheduledCount, failedCount, existingJobs.Count);

            if (failedCount > 0 && scheduledCount == 0)
            {
                throw new InvalidOperationException(
                    $"Failed to schedule all {failedCount} jobs. See logs for details.");
            }

            return new HistoricalSeasonSourcingResponse
            {
                CorrelationId = existingSeasonJob.CreatedBy,
                Message = BuildForceRescheduleMessage(scheduledCount, failedCount, existingJobs.Count)
            };
        }
        finally
        {
            if (lockAcquired)
            {
                await ReleaseAdvisoryLockAsync(lockId);
            }
        }
    }

    /// <summary>
    /// Tries to acquire a PostgreSQL advisory lock for the given lock ID.
    /// </summary>
    private async Task<bool> TryAcquireAdvisoryLockAsync(long lockId, CancellationToken cancellationToken)
    {
        return await _dataContext.Database
            .SqlQueryRaw<bool>("SELECT pg_try_advisory_lock({0})", lockId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Handles the case where a concurrent force request is already in progress.
    /// </summary>
    private HistoricalSeasonSourcingResponse HandleConcurrentForceRequest(
        HistoricalSeasonSourcingRequest request,
        Guid correlationId,
        long lockId)
    {
        _logger.LogWarning(
            "Concurrent Force request detected. Another force reschedule is in progress. " +
            "Sport={Sport}, Provider={Provider}, Year={Year}, LockId={LockId}",
            request.Sport, request.SourceDataProvider, request.SeasonYear, lockId);

        return new HistoricalSeasonSourcingResponse
        {
            CorrelationId = correlationId,
            Message = "Another force reschedule is already in progress for this season. Please try again in a few minutes."
        };
    }

    /// <summary>
    /// Fetches all existing tier jobs for the specified season.
    /// </summary>
    private async Task<List<ResourceIndexEntity>> FetchExistingTierJobsAsync(
        HistoricalSeasonSourcingRequest request,
        CancellationToken cancellationToken)
    {
        return await _dataContext.ResourceIndexJobs
            .AsNoTracking()
            .Where(x =>
                x.Provider == request.SourceDataProvider &&
                x.SportId == request.Sport &&
                x.SeasonYear == request.SeasonYear &&
                (x.DocumentType == DocumentType.Season ||
                 x.DocumentType == DocumentType.Venue ||
                 x.DocumentType == DocumentType.TeamSeason ||
                 x.DocumentType == DocumentType.AthleteSeason))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Reschedules all tier jobs with error handling.
    /// Returns tuple of (scheduledCount, failedCount).
    /// </summary>
    private (int scheduledCount, int failedCount) RescheduleTierJobs(
        List<ResourceIndexEntity> jobs,
        TierDelays tierDelays)
    {
        var scheduledCount = 0;
        var failedCount = 0;

        foreach (var job in jobs)
        {
            try
            {
                var delayMinutes = job.DocumentType switch
                {
                    DocumentType.Season => tierDelays.Season,
                    DocumentType.Venue => tierDelays.Venue,
                    DocumentType.TeamSeason => tierDelays.TeamSeason,
                    DocumentType.AthleteSeason => tierDelays.AthleteSeason,
                    _ => LogUnexpectedDocumentType(job.DocumentType)
                };

                var delay = TimeSpan.FromMinutes(delayMinutes);
                var jobDefinition = new DocumentJobDefinition(job);

                _backgroundJobProvider.Schedule<ResourceIndexJob>(
                    j => j.ExecuteAsync(jobDefinition),
                    delay);

                scheduledCount++;

                _logger.LogInformation(
                    "Re-scheduled job for tier. DocumentType={DocumentType}, ResourceIndexId={ResourceIndexId}, Delay={Delay}",
                    job.DocumentType, job.Id, delay);
            }
            catch (Exception ex)
            {
                failedCount++;

                _logger.LogError(ex,
                    "Failed to schedule job for tier. DocumentType={DocumentType}, ResourceIndexId={ResourceIndexId}. " +
                    "Continuing with remaining jobs.",
                    job.DocumentType, job.Id);
            }
        }

        return (scheduledCount, failedCount);
    }

    /// <summary>
    /// Builds a descriptive message about the force reschedule operation.
    /// </summary>
    private static string BuildForceRescheduleMessage(int scheduledCount, int failedCount, int totalCount)
    {
        return failedCount > 0
            ? $"Force reschedule completed with {failedCount} failures. {scheduledCount}/{totalCount} jobs scheduled successfully."
            : $"Force reschedule completed successfully. {scheduledCount} jobs scheduled.";
    }

    /// <summary>
    /// Releases a PostgreSQL advisory lock.
    /// </summary>
    private async Task ReleaseAdvisoryLockAsync(long lockId)
    {
        try
        {
            await _dataContext.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_unlock({0})",
                lockId);

            _logger.LogInformation(
                "Force reschedule lock released. LockId={LockId}",
                lockId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to release force reschedule lock. LockId={LockId}. " +
                "Lock will be automatically released when database connection closes.",
                lockId);
        }
    }

    /// <summary>
    /// Creates new historical season sourcing by creating ResourceIndex records and scheduling tier jobs.
    /// </summary>
    private async Task<HistoricalSeasonSourcingResponse> CreateNewSeasonSourcingAsync(
        HistoricalSeasonSourcingRequest request,
        TierDelays tierDelays,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var tiers = DefineTiers(tierDelays);
        var baseOrdinal = GenerateBaseOrdinal();

        var resourceIndexes = await CreateResourceIndexRecordsAsync(
            request, tiers, baseOrdinal, correlationId, cancellationToken);

        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ResourceIndex records persisted. Scheduling {Count} jobs...", resourceIndexes.Count);

        var (scheduledCount, failedCount) = ScheduleTierJobs(resourceIndexes);

        if (failedCount > 0)
        {
            _logger.LogWarning(
                "Historical season sourcing completed with scheduling failures. " +
                "ScheduledJobs={ScheduledCount}, FailedJobs={FailedCount}, TotalJobs={TotalJobs}, CorrelationId={CorrelationId}",
                scheduledCount, failedCount, resourceIndexes.Count, correlationId);
        }
        else
        {
            _logger.LogInformation(
                "Historical season sourcing initiated successfully. {TierCount} tiers scheduled. CorrelationId={CorrelationId}",
                tiers.Length, correlationId);
        }

        return new HistoricalSeasonSourcingResponse
        {
            CorrelationId = correlationId
        };
    }

    /// <summary>
    /// Defines the tier configuration for historical sourcing.
    /// </summary>
    private static TierDefinition[] DefineTiers(TierDelays tierDelays)
    {
        return
        [
            new TierDefinition(DocumentType.Season, ResourceShape.Leaf, tierDelays.Season),
            new TierDefinition(DocumentType.Venue, ResourceShape.Index, tierDelays.Venue),
            new TierDefinition(DocumentType.TeamSeason, ResourceShape.Index, tierDelays.TeamSeason),
            new TierDefinition(DocumentType.AthleteSeason, ResourceShape.Index, tierDelays.AthleteSeason)
        ];
    }

    /// <summary>
    /// Generates a timestamp-based ordinal for ResourceIndex records.
    /// Format: YYYYMMDDHHmmssfff (17 digits)
    /// </summary>
    private static long GenerateBaseOrdinal()
    {
        var startTime = DateTime.UtcNow;
        return long.Parse(startTime.ToString("yyyyMMddHHmmssfff"));
    }

    /// <summary>
    /// Creates ResourceIndex records for all tiers and returns them with their delays for scheduling.
    /// </summary>
    private async Task<List<(ResourceIndexEntity Entity, TimeSpan Delay)>> CreateResourceIndexRecordsAsync(
        HistoricalSeasonSourcingRequest request,
        TierDefinition[] tiers,
        long baseOrdinal,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        var result = new List<(ResourceIndexEntity Entity, TimeSpan Delay)>();

        for (var i = 0; i < tiers.Length; i++)
        {
            var tier = tiers[i];
            var uri = _uriBuilder.BuildUri(tier.DocumentType, request.SeasonYear, request.Sport, request.SourceDataProvider);

            var resourceIndex = new ResourceIndexEntity
            {
                Id = Guid.NewGuid(),
                Ordinal = baseOrdinal * 100L + i,
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
                CreatedBy = correlationId
            };

            await _dataContext.ResourceIndexJobs.AddAsync(resourceIndex, cancellationToken);

            var delayTimeSpan = TimeSpan.FromMinutes(tier.DelayMinutes);
            result.Add((resourceIndex, delayTimeSpan));

            _logger.LogInformation(
                "Created ResourceIndex for tier. Tier={TierName}, DocumentType={DocumentType}, Delay={DelayMinutes}min, " +
                "Ordinal={Ordinal}, ResourceIndexId={ResourceIndexId}, Uri={Uri}",
                tier.DocumentType, tier.DocumentType, tier.DelayMinutes,
                resourceIndex.Ordinal, resourceIndex.Id, uri);
        }

        return result;
    }

    /// <summary>
    /// Schedules background jobs for all tier ResourceIndex records with error handling.
    /// Continues scheduling remaining jobs even if individual jobs fail.
    /// Returns tuple of (scheduledCount, failedCount).
    /// </summary>
    private (int scheduledCount, int failedCount) ScheduleTierJobs(List<(ResourceIndexEntity Entity, TimeSpan Delay)> resourceIndexes)
    {
        var scheduledCount = 0;
        var failedCount = 0;

        foreach (var (resourceIndex, delay) in resourceIndexes)
        {
            try
            {
                var jobDefinition = new DocumentJobDefinition(resourceIndex);

                _backgroundJobProvider.Schedule<ResourceIndexJob>(
                    job => job.ExecuteAsync(jobDefinition),
                    delay);

                scheduledCount++;

                _logger.LogInformation(
                    "Scheduled job for tier. DocumentType={DocumentType}, ResourceIndexId={ResourceIndexId}, Delay={Delay}",
                    resourceIndex.DocumentType, resourceIndex.Id, delay);
            }
            catch (Exception ex)
            {
                failedCount++;

                _logger.LogError(ex,
                    "Failed to schedule job for tier. DocumentType={DocumentType}, ResourceIndexId={ResourceIndexId}, Delay={Delay}. " +
                    "Continuing with remaining jobs.",
                    resourceIndex.DocumentType, resourceIndex.Id, delay);
            }
        }

        return (scheduledCount, failedCount);
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

    /// <summary>
    /// Logs a warning when an unexpected DocumentType is encountered and returns default delay of 0.
    /// This helps identify potential data inconsistencies or configuration issues.
    /// </summary>
    /// <param name="documentType">The unexpected document type</param>
    /// <returns>Default delay of 0 minutes</returns>
    private int LogUnexpectedDocumentType(DocumentType documentType)
    {
        _logger.LogWarning(
            "Unexpected DocumentType encountered in historical sourcing. " +
            "DocumentType={DocumentType}. Using default delay of 0 minutes. " +
            "This may indicate a data inconsistency or missing configuration.",
            documentType);
        return 0;
    }

    /// <summary>
    /// Generates a stable PostgreSQL advisory lock ID from season/provider/sport combination.
    /// Uses SHA256 hashing to ensure consistent lock IDs across application restarts and different runtimes.
    /// PostgreSQL advisory locks use bigint (64-bit) IDs.
    /// </summary>
    /// <param name="provider">Data provider</param>
    /// <param name="sport">Sport type</param>
    /// <param name="seasonYear">Season year</param>
    /// <returns>Stable 64-bit advisory lock ID (always positive)</returns>
    private static long GetAdvisoryLockId(SourceDataProvider provider, Sport sport, int seasonYear)
    {
        // Combine into a unique string
        var lockString = $"HistoricalSeasonForce:{provider}:{sport}:{seasonYear}";
        
        // Compute SHA256 hash for stable, deterministic hashing
        var bytes = Encoding.UTF8.GetBytes(lockString);
        var hashBytes = SHA256.HashData(bytes);
        
        // Take first 8 bytes and convert to 64-bit integer
        var lockId = BitConverter.ToInt64(hashBytes, 0);
        
        // Normalize to positive value (PostgreSQL advisory locks support negative, but positive is clearer)
        // Handle Int64.MinValue edge case: use & with long.MaxValue to avoid overflow
        return lockId == long.MinValue ? long.MaxValue : Math.Abs(lockId);
    }

    /// <summary>
    /// Creates ResourceIndex entities for all 4 tiers WITHOUT scheduling them in Hangfire.
    /// Used by saga-based orchestration where the saga triggers each tier via events.
    /// </summary>
    public async Task<Guid> CreateSagaResourceIndexesAsync(
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
                "Creating ResourceIndex entities for saga orchestration. Sport={Sport}, Provider={Provider}, Year={Year}",
                request.Sport, request.SourceDataProvider, request.SeasonYear);

            // Define all 4 tiers (delays don't matter for saga - saga controls timing)
            var tiers = new[]
            {
                (DocumentType.Season, ResourceShape.Auto),
                (DocumentType.Venue, ResourceShape.Index),
                (DocumentType.TeamSeason, ResourceShape.Index),
                (DocumentType.AthleteSeason, ResourceShape.Index)
            };

            var baseOrdinal = GenerateBaseOrdinal();

            for (var i = 0; i < tiers.Length; i++)
            {
                var (docType, shape) = tiers[i];
                var uri = _uriBuilder.BuildUri(docType, request.SeasonYear, request.Sport, request.SourceDataProvider);

                var resourceIndex = new ResourceIndexEntity
                {
                    Id = Guid.NewGuid(),
                    Ordinal = baseOrdinal * 100L + i,
                    Name = _routingKeyGenerator.Generate(request.SourceDataProvider, uri),
                    IsRecurring = false,
                    IsQueued = false,
                    CronExpression = null,
                    IsEnabled = true,
                    Provider = request.SourceDataProvider,
                    DocumentType = docType,
                    Shape = shape,
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
                    CreatedBy = correlationId  // Saga will use this to find its jobs
                };

                await _dataContext.ResourceIndexJobs.AddAsync(resourceIndex, cancellationToken);

                _logger.LogInformation(
                    "Created ResourceIndex for saga tier. Tier={TierName}, DocumentType={DocumentType}, " +
                    "Ordinal={Ordinal}, ResourceIndexId={ResourceIndexId}, Uri={Uri}",
                    docType, docType, resourceIndex.Ordinal, resourceIndex.Id, uri);
            }

            await _dataContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "✅ All 4 ResourceIndex entities created for saga. CorrelationId={CorrelationId}",
                correlationId);

            return correlationId;
        }
    }

    private record TierDefinition(DocumentType DocumentType, ResourceShape Shape, int DelayMinutes);
}
