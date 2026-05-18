using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions;

/// <summary>
/// Sport-neutral live-competition streamer. Drives the ESPN polling lifecycle,
/// status state machine, and child-document DocumentRequested fan-out. Per-sport
/// subclasses supply the typed parent-competition DTO and the polling target list
/// (which child docs to poll, and at what cadence).
/// </summary>
/// <typeparam name="TCompetitionDto">The sport-specific parent-competition DTO
/// (e.g. EspnFootballEventCompetitionDto, EspnBaseballEventCompetitionDto).</typeparam>
public abstract class CompetitionStreamerBase<TCompetitionDto> : ICompetitionBroadcastingJob
    where TCompetitionDto : EspnEventCompetitionDtoBase
{
    private readonly ILogger _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly HttpClient _httpClient;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    private readonly List<Task> _activeWorkers = new();
    private CancellationTokenSource? _workerCts;

    private static readonly TimeSpan MaxStreamDuration = TimeSpan.FromHours(5);
    private const int MaxConsecutiveFailures = 10;

    /// <summary>
    /// Why WaitForLiveStartAsync stopped. Drives whether ExecuteAsync proceeds
    /// to spawn polling workers (StartDetected) or short-circuits to a
    /// terminal state (AlreadyFinal, Timeout).
    /// </summary>
    protected enum LiveStartOutcome
    {
        StartDetected = 0,
        AlreadyFinal = 1,
        Timeout = 2,
    }

    /// <summary>
    /// Why PollWhileInProgressAsync stopped. Final = normal competition end (mark
    /// Completed); Timeout = stream exceeded MaxStreamDuration without seeing
    /// STATUS_FINAL (mark Failed with reason — likely indicates an abandoned
    /// stream or upstream data anomaly worth investigating).
    /// </summary>
    protected enum PollOutcome
    {
        Final = 0,
        Timeout = 1,
    }

    protected CompetitionStreamerBase(
        ILogger logger,
        TeamSportDataContext dataContext,
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _httpClient = httpClientFactory.CreateClient();
        _scopeFactory = scopeFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Sport-specific polling targets. Each entry is (child-doc URI, doc type, polling interval
    /// seconds, requires-parent-id flag). Returning a null URI signals that the parent doc lacks
    /// that child link for this competition; the worker is silently skipped. Subclasses should
    /// keep the list short — every entry adds load to ESPN.
    ///
    /// <para>
    /// <c>RequiresParentId</c> controls whether the published <c>DocumentRequested</c> message
    /// carries <c>ParentId = CompetitionId</c>. Set <c>true</c> when the eventual document
    /// processor calls <c>TryGetOrDeriveParentId</c> (Drive, Play, Situation, Leaders); set
    /// <c>false</c> when the processor resolves its parent through its own DTO link (Probability).
    /// Declaring this per-target keeps sport-specific DocumentType knowledge out of the base.
    /// </para>
    /// </summary>
    protected abstract IEnumerable<(Uri? RefUri, DocumentType DocumentType, int IntervalSeconds, bool RequiresParentId)>
        GetPollingTargets(TCompetitionDto competitionDto);

    public async Task ExecuteAsync(StreamCompetitionCommand command, CancellationToken cancellationToken)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["Sport"] = command.Sport,
            ["SeasonYear"] = command.SeasonYear,
            ["ContestId"] = command.ContestId,
            ["CorrelationId"] = command.CorrelationId,
            ["CompetitionId"] = command.CompetitionId
        }))
        {
            _logger.LogInformation("Broadcasting job started for {@Command}", command);

            CompetitionStream? stream = null;

            try
            {
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

                stream = await _dataContext.CompetitionStreams
                    .FirstOrDefaultAsync(x => x.CompetitionId == command.CompetitionId, cancellationToken);

                if (stream != null)
                {
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.AwaitingStart, cancellationToken);
                }
                else
                {
                    _logger.LogWarning("No CompetitionStream record found for competition. Status tracking will be skipped.");
                }

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

                switch (status.Type.Name)
                {
                    case "STATUS_SCHEDULED":
                        _logger.LogInformation("Competition is scheduled. Waiting for competition to start...");
                        var startOutcome = await WaitForLiveStartAsync(statusUri, cancellationToken);
                        switch (startOutcome)
                        {
                            case LiveStartOutcome.StartDetected:
                                break;

                            case LiveStartOutcome.AlreadyFinal:
                                _logger.LogInformation("Competition went final while waiting for start. Skipping live polling.");
                                if (stream != null)
                                {
                                    stream.StreamEndedUtc = _dateTimeProvider.UtcNow();
                                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Completed, cancellationToken);
                                }
                                return;

                            case LiveStartOutcome.Timeout:
                                _logger.LogWarning("Live start was not detected within max stream duration. Aborting.");
                                if (stream != null)
                                {
                                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, "Live start not detected within max stream duration");
                                }
                                return;
                        }
                        break;

                    case "STATUS_IN_PROGRESS":
                        _logger.LogInformation("Competition already in progress. Starting workers immediately.");
                        break;

                    case "STATUS_FINAL":
                        _logger.LogInformation("Competition already final. Skipping streaming.");
                        if (stream != null)
                        {
                            stream.StreamEndedUtc = _dateTimeProvider.UtcNow();
                            await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Completed, cancellationToken);
                        }
                        return;

                    default:
                        // An unknown status string is anomalous data — bail rather than
                        // optimistically spawning live workers against an unverified competition state.
                        _logger.LogWarning("Unknown status type: {StatusType}. Aborting stream.", status.Type.Name);
                        if (stream != null)
                        {
                            await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, $"Unknown status type: {status.Type.Name}");
                        }
                        return;
                }

                _logger.LogInformation("Starting polling workers for live competition updates");

                if (stream != null)
                {
                    stream.StreamStartedUtc = _dateTimeProvider.UtcNow();
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Active, cancellationToken);
                }

                _workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                StartPollingWorkers(competitionDto, command, _workerCts.Token);

                var pollOutcome = await PollWhileInProgressAsync(statusUri, _workerCts.Token);

                _logger.LogInformation("Polling loop exited with outcome {Outcome}. Stopping workers gracefully.", pollOutcome);
                if (stream != null)
                {
                    stream.StreamEndedUtc = _dateTimeProvider.UtcNow();
                    switch (pollOutcome)
                    {
                        case PollOutcome.Final:
                            await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Completed, cancellationToken);
                            break;

                        case PollOutcome.Timeout:
                            await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, "Stream exceeded max duration without STATUS_FINAL");
                            break;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Streaming cancelled by external request");
                if (stream != null)
                {
                    stream.StreamEndedUtc = _dateTimeProvider.UtcNow();
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, CancellationToken.None, "Cancelled by external request");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during streaming");
                if (stream != null)
                {
                    stream.StreamEndedUtc = _dateTimeProvider.UtcNow();
                    await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, CancellationToken.None, ex.Message);
                }
                throw;
            }
            finally
            {
                await StopWorkersAsync();
            }
        }
    }

    private async Task<TCompetitionDto?> GetCompetitionAsync(Uri uri, CancellationToken cancellationToken)
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
            return json.FromJson<TCompetitionDto>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get competition from {Uri}", uri);
            return null;
        }
    }

    private async Task<EspnEventCompetitionStatusDtoBase?> GetStatusAsync(Uri uri, CancellationToken cancellationToken)
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
            return json.FromJson<EspnEventCompetitionStatusDtoBase>();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Failed to get status from {Uri}", uri);
            return null;
        }
    }

    private async Task<LiveStartOutcome> WaitForLiveStartAsync(Uri statusUri, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Competition is scheduled. Polling for live start every 20 seconds...");

        var startTime = _dateTimeProvider.UtcNow();
        var consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_dateTimeProvider.UtcNow() - startTime > MaxStreamDuration)
            {
                _logger.LogWarning("Waiting for live start exceeded max duration ({Hours} hours). Stopping.", MaxStreamDuration.TotalHours);
                return LiveStartOutcome.Timeout;
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
                _logger.LogInformation("Live start detected. Competition is now in progress.");
                return LiveStartOutcome.StartDetected;
            }

            if (status.Type.Name == "STATUS_FINAL")
            {
                _logger.LogWarning("Competition marked final before starting. Exiting.");
                return LiveStartOutcome.AlreadyFinal;
            }

            _logger.LogDebug("Competition still scheduled. Status: {Status}", status.Type.Name);
        }

        // Cancellation observed by the while-condition (rather than via Task.Delay's
        // throwing overload). Treat as timeout-equivalent — caller's outer catch handles
        // the throwing case explicitly; reaching here means we exited cleanly without
        // a live-start signal, and we should not pretend the competition started.
        return LiveStartOutcome.Timeout;
    }

    private void StartPollingWorkers(
        TCompetitionDto competitionDto,
        StreamCompetitionCommand command,
        CancellationToken cancellationToken)
    {
        var refs = GetPollingTargets(competitionDto).ToList();

        _logger.LogInformation("Spawning {Count} polling workers", refs.Count);

        foreach (var (refUri, docType, intervalSeconds, requiresParentId) in refs)
        {
            if (refUri == null)
            {
                _logger.LogWarning("Skipping worker for {DocumentType} - URI is null", docType);
                continue;
            }

            SpawnPollingWorker(
                () => PublishDocumentRequestAsync(refUri, docType, requiresParentId, command, cancellationToken),
                intervalSeconds,
                docType,
                cancellationToken);
        }

        _logger.LogInformation("All workers spawned successfully. Active workers: {Count}", _activeWorkers.Count);
    }

    private async Task<PollOutcome> PollWhileInProgressAsync(Uri statusUri, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monitoring competition status every 30 seconds until completion...");

        var startTime = _dateTimeProvider.UtcNow();
        var consecutiveFailures = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_dateTimeProvider.UtcNow() - startTime > MaxStreamDuration)
            {
                _logger.LogWarning("Stream exceeded max duration ({Hours} hours). Stopping.", MaxStreamDuration.TotalHours);
                return PollOutcome.Timeout;
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
                _logger.LogInformation("Competition status is FINAL. Ending stream.");
                return PollOutcome.Final;
            }

            _logger.LogDebug("Competition still in progress. Status: {Status}, Clock: {Clock}",
                status.Type.Name, status.DisplayClock);
        }

        // Cancellation observed by the while-condition. Treat as timeout-equivalent
        // (no STATUS_FINAL was observed). The throwing-cancellation path is handled
        // by ExecuteAsync's outer OperationCanceledException catch.
        return PollOutcome.Timeout;
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
            _workerCts?.Cancel();

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
        bool requiresParentId,
        StreamCompetitionCommand command,
        CancellationToken cancellationToken)
    {
        var parentId = requiresParentId ? command.CompetitionId.ToString() : null;

        _logger.LogDebug("Publishing {Type} document request for {Uri}", type, refUri);

        // Workers run concurrently with each other and with the main thread that
        // owns _dataContext. EF Core DbContext is not thread-safe, so each poll
        // resolves its own scope (and therefore its own DbContext + IEventBus
        // pair). The MassTransit EF outbox interceptor flushes pending publishes
        // on SaveChangesAsync within this scope's DbContext — so resolving the
        // bus from the same scope as the context is required for transactional
        // outbox semantics.
        using var scope = _scopeFactory.CreateScope();
        var dataContext = scope.ServiceProvider.GetRequiredService<TeamSportDataContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IEventBus>();

        await publishEndpoint.Publish(new DocumentRequested(
            Id: Guid.NewGuid().ToString(),
            ParentId: parentId,
            Uri: refUri,
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.SeasonYear,
            DocumentType: type,
            SourceDataProvider: command.DataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: command.CorrelationId
        ), cancellationToken);

        await dataContext.SaveChangesAsync(cancellationToken);
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

        stream.ModifiedUtc = _dateTimeProvider.UtcNow();
        stream.ModifiedBy = Guid.Empty;

        await _dataContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Stream status updated to {Status}", status);
    }
}
