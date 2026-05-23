using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

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
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;
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
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        IDateTimeProvider dateTimeProvider)
    {
        _logger = logger;
        _dataContext = dataContext;
        _httpClient = httpClientFactory.CreateClient();
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
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
        // RunId distinguishes individual executions of the same stream (Hangfire
        // refire, admin replay, pod restart). The CorrelationId rebound below is
        // CompetitionStream.Id and is stable across runs; RunId is fresh per run.
        var runId = Guid.NewGuid();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["Sport"] = command.Sport,
            ["SeasonYear"] = command.SeasonYear,
            ["ContestId"] = command.ContestId,
            ["CorrelationId"] = command.CorrelationId,
            ["CompetitionId"] = command.CompetitionId,
            ["RunId"] = runId
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

                if (stream == null)
                {
                    _logger.LogError("No CompetitionStream record found for competition. Status tracking will be skipped.");
                    return;
                }

                await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.AwaitingStart, cancellationToken);

                // Rebind CorrelationId to stream.Id so a single Seq query finds
                // every log line across Producer (streamer + document processors)
                // and Provider (DocumentRequestedHandler + sourcing pipeline) for
                // this stream's lifetime — including refires/restarts of the
                // same stream. The DocumentRequested / ContestCompleted publishes
                // also carry stream.Id as CorrelationId so downstream services
                // see the same id in their resolved fallback chain.
                using (_logger.BeginScope(new Dictionary<string, object>
                {
                    ["CorrelationId"] = stream.Id,
                    ["StreamId"] = stream.Id
                }))
                {
                    _logger.LogInformation(
                        "CompetitionStream loaded. StreamId={StreamId}; CorrelationId rebound from {PriorCorrelationId} to StreamId for the remainder of this run.",
                        stream.Id, command.CorrelationId);

                    await ExecuteWithStreamAsync(command, competition, externalId, stream, cancellationToken);
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

    /// <summary>
    /// Post-stream-load streaming lifecycle. Called from inside the nested log
    /// scope in <see cref="ExecuteAsync"/> that rebinds CorrelationId to
    /// <c>stream.Id</c>, so every log line written from here forward (including
    /// from the polling loops + publish helpers) carries the stream-scoped
    /// CorrelationId. Exceptions propagate to <see cref="ExecuteAsync"/>'s
    /// outer try/catch.
    /// </summary>
    private async Task ExecuteWithStreamAsync(
        StreamCompetitionCommand command,
        CompetitionBase competition,
        CompetitionExternalId externalId,
        CompetitionStream stream,
        CancellationToken cancellationToken)
    {
        var competitionDto = await GetCompetitionAsync(new Uri(externalId.SourceUrl), cancellationToken);
        if (competitionDto == null)
        {
            _logger.LogError("Competition fetch failed from ESPN");
            await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, "Competition fetch failed");
            return;
        }

        var statusUri = EspnUriMapper.CompetitionRefToCompetitionStatusRef(new Uri(externalId.SourceUrl));

        var status = await GetStatusAsync(statusUri, cancellationToken);
        if (status == null)
        {
            _logger.LogError("Initial status fetch failed");
            await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, "Initial status fetch failed");
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
                        // The initial competitionDto fetch above happened while the
                        // game was still STATUS_SCHEDULED, so ESPN had not yet
                        // populated the live-data refs (Plays, Probability,
                        // Situation, Leaders) on the parent EventCompetition
                        // payload. Re-fetch now that the game is in progress so
                        // GetPollingTargets returns real URIs instead of nulls.
                        // Without this refresh, every poller logs "URI is null",
                        // StartPollingWorkers spawns Active workers: 0, and the
                        // monitoring loop runs silently for the full game.
                        competitionDto = await GetCompetitionAsync(new Uri(externalId.SourceUrl), cancellationToken);
                        if (competitionDto == null)
                        {
                            _logger.LogError("Competition re-fetch after live start failed");
                            await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, "Competition re-fetch after live start failed");
                            return;
                        }
                        break;

                    case LiveStartOutcome.AlreadyFinal:
                        _logger.LogInformation("Competition went final while waiting for start. Skipping live polling.");
                        await PublishContestCompletedAsync(stream.Id, command, competition.Contest!.SeasonWeekId, cancellationToken);
                        await PublishContestRefreshOnFinalAsync(stream.Id, new Uri(externalId.SourceUrl), command, cancellationToken);
                        stream.StreamEndedUtc = _dateTimeProvider.UtcNow();
                        await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Completed, cancellationToken);
                        return;

                    case LiveStartOutcome.Timeout:
                        _logger.LogWarning("Live start was not detected within max stream duration. Aborting.");
                        await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, "Live start not detected within max stream duration");
                        return;
                }
                break;

            case "STATUS_IN_PROGRESS":
                _logger.LogInformation("Competition already in progress. Starting workers immediately.");
                break;

            case "STATUS_FINAL":
                _logger.LogInformation("Competition already final. Skipping streaming.");
                await PublishContestCompletedAsync(stream.Id, command, competition.Contest!.SeasonWeekId, cancellationToken);
                await PublishContestRefreshOnFinalAsync(stream.Id, new Uri(externalId.SourceUrl), command, cancellationToken);
                stream.StreamEndedUtc = _dateTimeProvider.UtcNow();
                await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Completed, cancellationToken);
                return;

            default:
                // An unknown status string is anomalous data — bail rather than
                // optimistically spawning live workers against an unverified competition state.
                _logger.LogWarning("Unknown status type: {StatusType}. Aborting stream.", status.Type.Name);
                await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Failed, cancellationToken, $"Unknown status type: {status.Type.Name}");
                return;
        }

        _logger.LogInformation("Starting polling workers for live competition updates");

        stream.StreamStartedUtc = _dateTimeProvider.UtcNow();
        await UpdateStreamStatusAsync(stream, CompetitionStreamStatus.Active, cancellationToken);

        _workerCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        StartPollingWorkers(competitionDto, stream.Id, command, _workerCts.Token);

        var pollOutcome = await PollWhileInProgressAsync(statusUri, _workerCts.Token);

        _logger.LogInformation("Polling loop exited with outcome {Outcome}. Stopping workers gracefully.", pollOutcome);
        if (pollOutcome == PollOutcome.Final)
        {
            await PublishContestCompletedAsync(stream.Id, command, competition.Contest!.SeasonWeekId, cancellationToken);
            await PublishContestRefreshOnFinalAsync(stream.Id, new Uri(externalId.SourceUrl), command, cancellationToken);
        }

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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Genuine caller-initiated cancellation — propagate so the outer
            // OperationCanceledException catch in ExecuteAsync runs.
            throw;
        }
        catch (Exception ex)
        {
            // Includes HttpClient.Timeout (TaskCanceledException with no caller-token
            // cancellation), socket errors, JSON parse failures. Surface as a null
            // result so the polling loops' consecutiveFailures logic handles it.
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
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Genuine caller-initiated cancellation — propagate so the outer
            // OperationCanceledException catch in ExecuteAsync runs.
            throw;
        }
        catch (Exception ex)
        {
            // Includes HttpClient.Timeout (TaskCanceledException with no caller-token
            // cancellation), socket errors, JSON parse failures. Surface as a null
            // result so the polling loops' consecutiveFailures logic handles it.
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
        Guid correlationId,
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
                () => PublishDocumentRequestAsync(correlationId, refUri, docType, requiresParentId, command, cancellationToken),
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
        var tickCount = 0;
        // Info heartbeat every Nth tick so Seq has proof-of-life during long
        // in-game stretches without flooding at every 30s poll. At 10 ticks
        // (~5 min) a healthy stream emits 12 heartbeats per hour — readable
        // in Seq, distinguishable from a hang.
        const int HeartbeatEveryNTicks = 10;

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

            tickCount++;
            if (tickCount % HeartbeatEveryNTicks == 0)
            {
                _logger.LogInformation(
                    "Streaming heartbeat. Tick={Tick}, ElapsedMin={ElapsedMin:F1}, Status={Status}, Clock={Clock}",
                    tickCount,
                    (_dateTimeProvider.UtcNow() - startTime).TotalMinutes,
                    status.Type.Name,
                    status.DisplayClock);
            }
            else
            {
                _logger.LogDebug("Competition still in progress. Status: {Status}, Clock: {Clock}",
                    status.Type.Name, status.DisplayClock);
            }
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

    /// <summary>
    /// Publish ContestCompleted at any STATUS_FINAL detection point so the API
    /// scoring path can trigger immediately rather than waiting on the daily
    /// cron backstop.
    ///
    /// Uses <see cref="DeliveryMode.Direct"/> because this is a stateless
    /// publish — no entity write happens inside this method, so the EF outbox
    /// interceptor wouldn't have a SaveChangesAsync hook to ride. Direct
    /// delivery sidesteps the outbox entirely and hands the message to the
    /// broker immediately. Same pattern as
    /// <see cref="Application.Contests.BaseballContestReplayService"/>.
    ///
    /// Idempotency on the consumer side handles at-least-once redelivery and
    /// the three concurrent publish sites in <see cref="ExecuteAsync"/> —
    /// downstream ContestScoringProcessor short-circuits when no UserPicks for
    /// the contest are unscored.
    /// </summary>
    private async Task PublishContestCompletedAsync(
        Guid correlationId,
        StreamCompetitionCommand command,
        Guid? seasonWeekId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Publishing ContestCompleted for ContestId={ContestId}, CompetitionId={CompetitionId}, CorrelationId={CorrelationId}",
            command.ContestId, command.CompetitionId, correlationId);

        using var _ = _deliveryScope.Use(DeliveryMode.Direct);
        await _eventBus.Publish(new ContestCompleted(
            ContestId: command.ContestId,
            CompetitionId: command.CompetitionId,
            SeasonWeekId: seasonWeekId,
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.SeasonYear,
            CorrelationId: correlationId,
            CausationId: correlationId
        ), cancellationToken);
    }

    /// <summary>
    /// Re-source the parent Event document at STATUS_FINAL so the canonical
    /// Contest entity gets its final state (IsFinal, completed status flags,
    /// final score totals) regardless of whether the in-progress poll loop
    /// caught the last update tick. Without this, a viewer that leaves the
    /// picks page open overnight sees stale "Live" status the next morning
    /// because the canonical entity never received the finalization write.
    ///
    /// <c>IncludeLinkedDocumentTypes</c> is intentionally omitted — at
    /// finalization we want every linked child type re-sourced, not the
    /// narrowed set used by the manual <c>ContestUpdateProcessor</c> path.
    /// Provider's re-publish suppression window (~90 min) absorbs the
    /// inevitable duplicates against documents we already polled during
    /// the stream, so asking for "everything" here is cheap.
    ///
    /// The Event URI is derived from the EventCompetition URI we already
    /// hold via <see cref="EspnUriMapper.CompetitionRefToContestRef"/>, so
    /// no extra DB roundtrip for Contest.ExternalIds is needed.
    /// </summary>
    private async Task PublishContestRefreshOnFinalAsync(
        Guid correlationId,
        Uri competitionRef,
        StreamCompetitionCommand command,
        CancellationToken cancellationToken)
    {
        var contestUri = EspnUriMapper.CompetitionRefToContestRef(competitionRef);

        _logger.LogInformation(
            "Publishing final Contest refresh DocumentRequested. ContestId={ContestId}, Uri={Uri}",
            command.ContestId, contestUri);

        using var _ = _deliveryScope.Use(DeliveryMode.Direct);
        await _eventBus.Publish(new DocumentRequested(
            Id: Guid.NewGuid().ToString(),
            ParentId: null,
            Uri: contestUri,
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.SeasonYear,
            DocumentType: DocumentType.Event,
            SourceDataProvider: command.DataProvider,
            CorrelationId: correlationId,
            CausationId: correlationId
        ), cancellationToken);
    }

    /// <summary>
    /// Fan out one DocumentRequested per polling worker tick.
    ///
    /// Stateless — no entity write — so we use <see cref="DeliveryMode.Direct"/>
    /// for the same reason as <see cref="PublishContestCompletedAsync"/>. The
    /// outbox interceptor only flushes on SaveChangesAsync against a non-empty
    /// change set; without that anchor, an outbox-mode publish would sit
    /// captured in the in-memory pending list and never reach the broker.
    ///
    /// <see cref="IEventBus"/> and <see cref="IMessageDeliveryScope"/> are
    /// thread-safe (MassTransit's IBus/IPublishEndpoint internals + an
    /// AsyncLocal-scoped policy respectively), so all polling workers can
    /// share the same injected instances without per-call scoping.
    /// </summary>
    private async Task PublishDocumentRequestAsync(
        Guid correlationId,
        Uri refUri,
        DocumentType type,
        bool requiresParentId,
        StreamCompetitionCommand command,
        CancellationToken cancellationToken)
    {
        var parentId = requiresParentId ? command.CompetitionId.ToString() : null;

        // Promoted from Debug to Info so steady-state worker activity is visible
        // at default filtering. ~3-6 publishes/min total across all polling
        // workers — not noisy. Closes the "healthy stream and hung stream look
        // identical in Seq" observability gap that masked the silent-publish
        // failure mode prior to PR #362.
        _logger.LogInformation("Publishing {Type} document request for {Uri}", type, refUri);

        using var _ = _deliveryScope.Use(DeliveryMode.Direct);
        await _eventBus.Publish(new DocumentRequested(
            Id: Guid.NewGuid().ToString(),
            ParentId: parentId,
            Uri: refUri,
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.SeasonYear,
            DocumentType: type,
            SourceDataProvider: command.DataProvider,
            CorrelationId: correlationId,
            CausationId: correlationId
        ), cancellationToken);
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
