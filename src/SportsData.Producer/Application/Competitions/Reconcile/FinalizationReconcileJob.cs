using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Competitions.Reconcile;

public class FinalizationReconcileJob<TDataContext> : IFinalizationReconcileJob
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<FinalizationReconcileJob<TDataContext>> _logger;
    private readonly TDataContext _dbContext;
    private readonly HttpClient _httpClient;
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IAppMode _appMode;

    // CompetitionStream rows started in the last 48h are candidates.
    // Older streams are assumed stale enough that publishing ContestCompleted
    // wouldn't accomplish anything (Contest already finalized via the daily
    // backstop, or stranded beyond what the consumer cares about). Bounds
    // ESPN polling cost per pass.
    private static readonly TimeSpan WindowOfConcern = TimeSpan.FromHours(48);

    // A stream still "in-progress" per ESPN that started > 12h ago is
    // anomalous (no live game runs that long) and surfaces a warning.
    private static readonly TimeSpan SuspectThreshold = TimeSpan.FromHours(12);

    public FinalizationReconcileJob(
        ILogger<FinalizationReconcileJob<TDataContext>> logger,
        TDataContext dbContext,
        IHttpClientFactory httpClientFactory,
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope,
        IDateTimeProvider dateTimeProvider,
        IAppMode appMode)
    {
        _logger = logger;
        _dbContext = dbContext;
        _httpClient = httpClientFactory.CreateClient();
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
        _dateTimeProvider = dateTimeProvider;
        _appMode = appMode;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();
        using var _ = _logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["Job"] = nameof(FinalizationReconcileJob<TDataContext>),
            ["Sport"] = _appMode.CurrentSport
        });

        var now = _dateTimeProvider.UtcNow();
        var lowerBound = now - WindowOfConcern;

        _logger.LogInformation(
            "FinalizationReconcileJob beginning. WindowLowerBound={LowerBound}", lowerBound);

        var stranded = await _dbContext.CompetitionStreams
            .AsNoTracking()
            .Include(s => s.Competition).ThenInclude(c => c.Contest)
            .Include(s => s.Competition).ThenInclude(c => c.ExternalIds)
            .Where(s => s.StreamStartedUtc != null &&
                        s.StreamStartedUtc > lowerBound &&
                        (s.Status == CompetitionStreamStatus.Active ||
                         s.Status == CompetitionStreamStatus.Failed))
            .ToListAsync(cancellationToken);

        // IsFinal is a computed property on ContestBase (FinalizedUtc.HasValue),
        // so it can't be translated to SQL — filter in memory after the
        // bounded query above.
        var actionable = stranded
            .Where(s => s.Competition?.Contest is { IsFinal: false })
            .ToList();

        _logger.LogInformation(
            "Found {StrandedCount} stranded streams; {ActionableCount} are unfinalized and require reconciliation.",
            stranded.Count, actionable.Count);

        var reconciled = 0;
        foreach (var stream in actionable)
        {
            if (cancellationToken.IsCancellationRequested) break;
            if (await TryReconcileAsync(stream, now, cancellationToken)) reconciled++;
        }

        _logger.LogInformation(
            "FinalizationReconcileJob complete. Reconciled={Reconciled} Candidates={Candidates}.",
            reconciled, actionable.Count);
    }

    private async Task<bool> TryReconcileAsync(
        CompetitionStream stream,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var externalId = stream.Competition.ExternalIds
            .FirstOrDefault(x => x.Provider == SourceDataProvider.Espn);

        if (externalId == null)
        {
            _logger.LogWarning(
                "Stranded stream has no ESPN external id; cannot reconcile. StreamId={StreamId}",
                stream.Id);
            return false;
        }

        var competitionRef = new Uri(externalId.SourceUrl);
        var statusUri = EspnUriMapper.CompetitionRefToCompetitionStatusRef(competitionRef);

        var status = await GetStatusAsync(statusUri, cancellationToken);
        if (status == null)
        {
            _logger.LogWarning(
                "ESPN status fetch returned null for stranded stream. StreamId={StreamId} StatusUri={StatusUri}",
                stream.Id, statusUri);
            return false;
        }

        if (status.Type?.Name == "STATUS_FINAL")
        {
            await PublishFinalizationEventsAsync(stream, competitionRef, cancellationToken);
            await MarkStreamCompletedAsync(stream.Id, now, cancellationToken);

            _logger.LogInformation(
                "Reconciled stranded stream → published ContestCompleted. StreamId={StreamId} ContestId={ContestId}",
                stream.Id, stream.Competition.ContestId);
            return true;
        }

        if (stream.StreamStartedUtc.HasValue &&
            stream.StreamStartedUtc.Value < now - SuspectThreshold)
        {
            _logger.LogWarning(
                "Stranded stream still in-progress per ESPN after {Hours:F1}h — anomalous. StreamId={StreamId} ContestId={ContestId} StatusName={StatusName}",
                (now - stream.StreamStartedUtc.Value).TotalHours,
                stream.Id,
                stream.Competition.ContestId,
                status.Type?.Name);
        }

        return false;
    }

    private async Task<EspnEventCompetitionStatusDtoBase?> GetStatusAsync(
        Uri statusUri,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _httpClient.GetAsync(statusUri, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return json.FromJson<EspnEventCompetitionStatusDtoBase>();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch ESPN status from {StatusUri}", statusUri);
            return null;
        }
    }

    private async Task PublishFinalizationEventsAsync(
        CompetitionStream stream,
        Uri competitionRef,
        CancellationToken cancellationToken)
    {
        // CorrelationId = stream.Id, matching the streamer's own publish path.
        // Causation = same value (this job is the originating cause in the
        // backstop chain). Direct delivery — no DbContext write happens here;
        // the MarkStreamCompletedAsync write is separated below to keep the
        // outbox interceptor out of the publish path. Same pattern as
        // CompetitionStreamerBase.PublishContestCompletedAsync.
        var correlationId = stream.Id;

        using (_deliveryScope.Use(DeliveryMode.Direct))
        {
            await _eventBus.Publish(new ContestCompleted(
                ContestId: stream.Competition.ContestId,
                CompetitionId: stream.CompetitionId,
                SeasonWeekId: stream.SeasonWeekId,
                Ref: null,
                Sport: _appMode.CurrentSport,
                SeasonYear: stream.Competition.Contest?.SeasonYear,
                CorrelationId: correlationId,
                CausationId: correlationId), cancellationToken);

            var contestUri = EspnUriMapper.CompetitionRefToContestRef(competitionRef);
            await _eventBus.Publish(new DocumentRequested(
                Id: Guid.NewGuid().ToString(),
                ParentId: null,
                Uri: contestUri,
                Ref: null,
                Sport: _appMode.CurrentSport,
                SeasonYear: stream.Competition.Contest?.SeasonYear,
                DocumentType: DocumentType.Event,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: correlationId,
                CausationId: correlationId), cancellationToken);
        }
    }

    private async Task MarkStreamCompletedAsync(
        Guid streamId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // The list-level query above is AsNoTracking() to keep memory low;
        // this scoped re-fetch is the only mutation path. The original
        // FailureReason is intentionally preserved as diagnostic evidence
        // for the original streamer miss.
        var tracked = await _dbContext.CompetitionStreams
            .FirstOrDefaultAsync(s => s.Id == streamId, cancellationToken);
        if (tracked == null) return;

        tracked.Status = CompetitionStreamStatus.Completed;
        tracked.StreamEndedUtc = now;
        tracked.ModifiedUtc = now;
        tracked.ModifiedBy = Guid.Empty;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
