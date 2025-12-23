using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Competitions;

public interface IFootballCompetitionBroadcastingJob
{
    Task ExecuteAsync(StreamFootballCompetitionCommand command, CancellationToken cancellationToken);
}

/// <summary>
/// This class is responsible for live-action updates for canonical data
/// and broadcasting those changes to downstream systems.
/// Phase 1 Refactoring: Added cancellation support, worker management, status tracking, and graceful shutdown.
/// </summary>
public class FootballCompetitionStreamer : IFootballCompetitionBroadcastingJob
{
    private readonly ILogger<FootballCompetitionStreamer> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly HttpClient _httpClient;
    private readonly IEventBus _publishEndpoint;

    // Worker management
    private readonly List<Task> _activeWorkers = new();
    private CancellationTokenSource? _workerCts;

    // Safety configuration
    private static readonly TimeSpan MaxStreamDuration = TimeSpan.FromHours(5);
    private const int MaxConsecutiveFailures = 10;

    public FootballCompetitionStreamer(
        ILogger<FootballCompetitionStreamer> logger,
        FootballDataContext dataContext,
        IHttpClientFactory httpClientFactory,
        IEventBus publishEndpoint)
    {
        _logger = logger;
        _dataContext = dataContext;
        _httpClient = httpClientFactory.CreateClient();
        _publishEndpoint = publishEndpoint;
    }

    public async Task ExecuteAsync(StreamFootballCompetitionCommand command, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId,
            ["CompetitionId"] = command.CompetitionId
        }))
        {
            _logger.LogInformation("Broadcasting job started for {@Command}", command);

            CompetitionStream? stream = null;

            try
            {
                // Load competition with related data
                var competition = await _dataContext.Competitions
                    .Include(c => c.Contest)
                    .Include(c => c.ExternalIds)
                    .Include(c => c.Competitors)
                        .ThenInclude(p => p.ExternalIds)
                    .Where(c => c.Id == command.CompetitionId)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(cancellationToken);

                if (competition == null)
                {
                    _logger.LogError("Competition not found");
                    return;
                }

                // Check if contest is final (client-side evaluation since IsFinal is computed)
                if (competition.Contest!.IsFinal == true)
                {
                    _logger.LogInformation("Contest is already final. Skipping streaming.");
                    return;
                }

                var externalId = competition.ExternalIds.FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);
                if (externalId == null)
                {
                    _logger.LogError("CompetitionExternalId not found for ESPN provider");
                    return;
                }

                // Load the stream record to track status (optional - may not exist in tests)
                stream = await _dataContext.CompetitionStreams
                    .FirstOrDefaultAsync(x => x.CompetitionId == command.CompetitionId, cancellationToken);

                // Update status to AwaitingStart
                if (stream != null)
                {
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.AwaitingStart, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("No CompetitionStream record found for CompetitionId {CompetitionId}. Status tracking will be skipped.", command.CompetitionId);
                }

                // Fetch the competition document to get child document URLs
                var competitionDto = await GetCompetitionAsync(new Uri(externalId.SourceUrl), cancellationToken);
                if (competitionDto == null)
                {
                    _logger.LogError("Competition fetch failed from ESPN");
                    if (stream != null)
                    {
                        await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, "Competition fetch failed");
                    }
                    return;
                }

                var statusUri = EspnUriMapper.CompetitionRefToCompetitionStatusRef(new Uri(externalId.SourceUrl));

                // Get initial status
                var status = await GetStatusAsync(statusUri, cancellationToken);
                if (status == null)
                {
                    _logger.LogError("Initial status fetch failed");
                    if (stream != null)
                    {
                        await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, "Initial status fetch failed");
                    }
                    return;
                }

                // Handle different initial game states
                switch (status.Type.Name)
                {
                    case "STATUS_SCHEDULED":
                        _logger.LogInformation("Game is scheduled. Waiting for kickoff...");
                        await WaitForKickoffAsync(statusUri, cancellationToken);
                        break;

                    case "STATUS_IN_PROGRESS":
                        _logger.LogInformation("Game already in progress. Starting workers immediately.");
                        break;

                    case "STATUS_FINAL":
                        _logger.LogInformation("Game already final. Skipping streaming.");
                        if (stream != null)
                        {
                            await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Completed, cancellationToken);
                        }
                        return;

                    default:
                        _logger.LogWarning("Unknown status type: {StatusType}", status.Type.Name);
                        break;
                }

                // Game is now in progress, update status and start workers
                _logger.LogInformation("Starting polling workers for live game updates");
                
                if (stream != null)
                {
                    stream.StreamStartedUtc = DateTime.UtcNow;
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Active, cancellationToken);
                }

                // Create worker cancellation token source linked to parent
                _workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                // Start polling workers
                StartPollingWorkers(competitionDto, command, _workerCts.Token);

                // Monitor status until game ends or cancellation
                await PollWhileInProgressAsync(statusUri, _workerCts.Token);

                // Game ended normally
                _logger.LogInformation("Game has ended. Stopping workers gracefully.");
                if (stream != null)
                {
                    stream.StreamEndedUtc = DateTime.UtcNow;
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Completed, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Streaming cancelled by external request");
                if (stream != null)
                {
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, CancellationToken.None, "Cancelled by external request");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during streaming");
                if (stream != null)
                {
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, CancellationToken.None, ex.Message);
                }
                throw;
            }
            finally
            {
                // Always clean up workers
                await StopWorkersAsync();
            }
        }
    }

    private async Task<EspnEventCompetitionDto?> GetCompetitionAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Competition fetch returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = json.FromJson<EspnEventCompetitionDto>();

            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get competition from {Uri}", uri);
            return null;
        }
    }

    private async Task<EspnEventCompetitionStatusDto?> GetStatusAsync(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(uri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Status fetch returned {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return json.FromJson<EspnEventCompetitionStatusDto>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get status from {Uri}", uri);
            return null;
        }
    }

    private async Task WaitForKickoffAsync(Uri statusUri, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Competition is scheduled. Polling for kickoff every 20 seconds...");

        var startTime = DateTime.UtcNow;
        var consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Safety timeout
            if (DateTime.UtcNow - startTime > MaxStreamDuration)
            {
                _logger.LogWarning("Waiting for kickoff exceeded max duration ({Hours} hours). Stopping.", MaxStreamDuration.TotalHours);
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(20), cancellationToken);

            var status = await GetStatusAsync(statusUri, cancellationToken);

            if (status == null)
            {
                consecutiveFailures++;
                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _logger.LogError("Too many consecutive failures fetching status ({Count}). Stopping.", consecutiveFailures);
                    throw new InvalidOperationException("Status polling failed repeatedly");
                }
                continue;
            }

            consecutiveFailures = 0;

            if (status.Type.Name == "STATUS_IN_PROGRESS")
            {
                _logger.LogInformation("Kickoff detected! Game is now in progress.");
                return;
            }

            if (status.Type.Name == "STATUS_FINAL")
            {
                _logger.LogWarning("Game marked final before starting. Exiting.");
                return;
            }

            _logger.LogDebug("Game still scheduled. Status: {Status}", status.Type.Name);
        }
    }

    private void StartPollingWorkers(
        EspnEventCompetitionDto competitionDto,
        StreamFootballCompetitionCommand command,
        CancellationToken cancellationToken)
    {
        var refs = new []
        {
            (competitionDto.Probabilities?.Ref, DocumentType.EventCompetitionProbability, 15),
            (competitionDto.Drives?.Ref, DocumentType.EventCompetitionDrive, 15),
            (competitionDto.Details?.Ref, DocumentType.EventCompetitionPlay, 10),
            (competitionDto.Situation?.Ref, DocumentType.EventCompetitionSituation, 5),
            (competitionDto.Leaders?.Ref, DocumentType.EventCompetitionLeaders, 60)
        };

        _logger.LogInformation("Spawning {Count} polling workers", refs.Length);

        foreach (var (refUri, docType, intervalSeconds) in refs)
        {
            if (refUri == null)
            {
                _logger.LogWarning("Skipping worker for {DocumentType} - URI is null", docType);
                continue;
            }

            SpawnPollingWorker(
                () => PublishDocumentRequestAsync(refUri, docType, command, cancellationToken),
                intervalSeconds,
                docType,
                cancellationToken);
        }

        _logger.LogInformation("All workers spawned successfully. Active workers: {Count}", _activeWorkers.Count);
    }

    private async Task PollWhileInProgressAsync(Uri statusUri, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monitoring game status every 30 seconds until completion...");

        var startTime = DateTime.UtcNow;
        var consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Safety timeout
            if (DateTime.UtcNow - startTime > MaxStreamDuration)
            {
                _logger.LogWarning("Stream exceeded max duration ({Hours} hours). Stopping.", MaxStreamDuration.TotalHours);
                break;
            }

            await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);

            var status = await GetStatusAsync(statusUri, cancellationToken);

            if (status == null)
            {
                consecutiveFailures++;
                _logger.LogWarning("Status fetch failed ({Count}/{Max})", consecutiveFailures, MaxConsecutiveFailures);
                
                if (consecutiveFailures >= MaxConsecutiveFailures)
                {
                    _logger.LogError("Too many consecutive failures. Stopping stream.");
                    throw new InvalidOperationException("Status polling failed repeatedly");
                }
                continue;
            }

            consecutiveFailures = 0;

            if (status.Type.Name == "STATUS_FINAL")
            {
                _logger.LogInformation("Game status is FINAL. Ending stream.");
                return;
            }

            _logger.LogDebug("Game still in progress. Status: {Status}, Clock: {Clock}", 
                status.Type.Name, status.DisplayClock);
        }
    }

    private void SpawnPollingWorker(
        Func<Task> taskFactory,
        int intervalSeconds,
        DocumentType documentType,
        CancellationToken cancellationToken)
    {
        var task = Task.Run(async () =>
        {
            _logger.LogInformation("Worker started for {DocumentType} (interval: {Interval}s)", documentType, intervalSeconds);

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        await taskFactory();
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex, "Worker for {DocumentType} failed during execution", documentType);
                    }

                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Worker for {DocumentType} cancelled gracefully", documentType);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker for {DocumentType} terminated unexpectedly", documentType);
            }
            finally
            {
                _logger.LogInformation("Worker for {DocumentType} stopped", documentType);
            }
        }, cancellationToken);

        _activeWorkers.Add(task);
    }

    private async Task StopWorkersAsync()
    {
        if (_activeWorkers.Count == 0)
        {
            _logger.LogDebug("No active workers to stop");
            return;
        }

        _logger.LogInformation("Stopping {Count} active workers...", _activeWorkers.Count);

        try
        {
            // Signal cancellation
            _workerCts?.Cancel();

            // Wait for all workers to complete (with timeout)
            var waitTask = Task.WhenAll(_activeWorkers);
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(10));

            var completedTask = await Task.WhenAny(waitTask, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _logger.LogWarning("Not all workers stopped within timeout. Proceeding anyway.");
            }
            else
            {
                _logger.LogInformation("All workers stopped successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while stopping workers");
        }
        finally
        {
            _activeWorkers.Clear();
            _workerCts?.Dispose();
            _workerCts = null;
        }
    }

    private async Task PublishDocumentRequestAsync(
        Uri refUri,
        DocumentType type,
        StreamFootballCompetitionCommand command,
        CancellationToken cancellationToken)
    {
        var parentId = type is
            DocumentType.EventCompetitionProbability or
            DocumentType.EventCompetitionDrive or
            DocumentType.EventCompetitionSituation
            ? command.CompetitionId.ToString()
            : null;

        _logger.LogDebug("Publishing {Type} document request for {Uri}", type, refUri);

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: Guid.NewGuid().ToString(),
            ParentId: parentId,
            Uri: refUri,
            Sport: command.Sport,
            SeasonYear: command.SeasonYear,
            DocumentType: type,
            SourceDataProvider: command.DataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: command.CorrelationId
        ), cancellationToken);

        await _dataContext.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateStreamStatusAsync(
        CompetitionStream stream,
        CompetitionStreamStatus status,
        CancellationToken cancellationToken,
        string? failureReason = null)
    {
        stream.Status = status;

        if (status == CompetitionStreamStatus.Failed && !string.IsNullOrWhiteSpace(failureReason))
        {
            stream.FailureReason = failureReason.Length > 512 
                ? failureReason.Substring(0, 512) 
                : failureReason;
        }

        stream.ModifiedUtc = DateTime.UtcNow;
        stream.ModifiedBy = Guid.Empty; // System modification

        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stream status updated to {Status}", status);
    }
}