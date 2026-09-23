using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using SportsData.Core.Common;
using SportsData.Core.DependencyInjection;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Processing;
using SportsData.Producer.Application.Franchises.Commands;
using SportsData.Producer.Application.FranchiseSeasons.Commands.CalculateFranchiseSeasonMetrics;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.FranchiseSeasons.Commands.EnqueueSingleFranchiseSeasonEnrichment;

public interface IEnqueueSingleFranchiseSeasonEnrichmentCommandHandler
{
    Task<Result<Guid>> ExecuteAsync(
        EnqueueSingleFranchiseSeasonEnrichmentCommand command,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// One franchise season, all three legs of "make it current", mirroring
/// <see cref="Franchises.FranchiseSeasonEnrichmentJob"/> leg for leg:
/// <list type="number">
///   <item>record enrichment (W/L from finalized contests) — a Hangfire job;</item>
///   <item>an ESPN season-statistics refresh — one scoped DocumentRequested
///     for the team's TeamSeason document, published DIRECT (the Producer's
///     ambient EF outbox would otherwise capture and discard it, since this
///     handler never saves);</item>
///   <item>metrics generation — football only, a Hangfire job.</item>
/// </list>
/// Returns the correlation id shared by all three, the operator's Seq handle.
/// Legs run in order; a failure on a later leg is reported as a failure even
/// though the earlier legs were already enqueued, because every leg is
/// idempotent and cheap to re-request from the same button.
/// </summary>
public class EnqueueSingleFranchiseSeasonEnrichmentCommandHandler : IEnqueueSingleFranchiseSeasonEnrichmentCommandHandler
{
    private readonly ILogger<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler> _logger;
    private readonly TeamSportDataContext _dataContext;
    private readonly IProvideBackgroundJobs _backgroundJobProvider;
    private readonly IAppMode _appMode;
    private readonly IEventBus _eventBus;
    private readonly IMessageDeliveryScope _deliveryScope;

    public EnqueueSingleFranchiseSeasonEnrichmentCommandHandler(
        ILogger<EnqueueSingleFranchiseSeasonEnrichmentCommandHandler> logger,
        TeamSportDataContext dataContext,
        IProvideBackgroundJobs backgroundJobProvider,
        IAppMode appMode,
        IEventBus eventBus,
        IMessageDeliveryScope deliveryScope)
    {
        _logger = logger;
        _dataContext = dataContext;
        _backgroundJobProvider = backgroundJobProvider;
        _appMode = appMode;
        _eventBus = eventBus;
        _deliveryScope = deliveryScope;
    }

    public async Task<Result<Guid>> ExecuteAsync(
        EnqueueSingleFranchiseSeasonEnrichmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.FranchiseSeasonId == Guid.Empty)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.Validation,
                [new ValidationFailure(nameof(command.FranchiseSeasonId), "FranchiseSeasonId is required.")]);
        }

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .Where(x => x.Id == command.FranchiseSeasonId)
            .Select(x => new
            {
                x.Id,
                x.SeasonYear,
                EspnRef = x.ExternalIds
                    .Where(e => e.Provider == SourceDataProvider.Espn)
                    .Select(e => new { e.SourceUrl, e.SourceUrlHash })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (franchiseSeason is null)
        {
            return new Failure<Guid>(
                default,
                ResultStatus.NotFound,
                [new ValidationFailure(nameof(command.FranchiseSeasonId),
                    $"FranchiseSeason {command.FranchiseSeasonId} not found.")]);
        }

        var correlationId = Guid.NewGuid();

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["FranchiseSeasonId"] = franchiseSeason.Id,
            ["SeasonYear"] = franchiseSeason.SeasonYear,
            ["Sport"] = _appMode.CurrentSport
        }))
        {
            try
            {
                // Leg 1: record enrichment.
                _backgroundJobProvider.Enqueue<IEnrichFranchiseSeasons>(
                    h => h.Process(new EnrichFranchiseSeasonCommand(
                        franchiseSeason.Id,
                        franchiseSeason.SeasonYear,
                        correlationId)));

                // Leg 2: season statistics refresh. Scoped to spawn ONLY the
                // statistics child of the TeamSeason document — same
                // vocabulary and same Direct delivery as the weekly job.
                if (franchiseSeason.EspnRef is not null
                    && Uri.TryCreate(franchiseSeason.EspnRef.SourceUrl, UriKind.Absolute, out var teamSeasonUri))
                {
                    var request = new DocumentRequested(
                        Id: franchiseSeason.EspnRef.SourceUrlHash,
                        ParentId: null,
                        Uri: teamSeasonUri,
                        Ref: null,
                        Sport: _appMode.CurrentSport,
                        SeasonYear: franchiseSeason.SeasonYear,
                        DocumentType: DocumentType.TeamSeason,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: correlationId,
                        CausationId: Guid.NewGuid(),
                        IncludeLinkedDocumentTypes: [DocumentType.TeamSeasonStatistics]);

                    // PublishBatch even for one message: the Direct-mode
                    // Publish pays a 1-second delay per call; the batch path
                    // does not (Vortex, PR #749).
                    using (_deliveryScope.Use(DeliveryMode.Direct))
                    {
                        await _eventBus.PublishBatch([request]);
                    }
                }
                else
                {
                    _logger.LogWarning(
                        "FranchiseSeason {FranchiseSeasonId} has no ESPN TeamSeason ref; statistics refresh skipped.",
                        franchiseSeason.Id);
                }

                // Leg 3: metrics, football only — the Calculate handler is
                // registered inside ServiceRegistration's football guard, so
                // an enqueue on a baseball pod would fail at activation.
                if (_appMode.CurrentSport is Sport.FootballNcaa or Sport.FootballNfl)
                {
                    var calculate = new CalculateFranchiseSeasonMetricsCommand(
                        franchiseSeason.Id,
                        franchiseSeason.SeasonYear);
                    _backgroundJobProvider.Enqueue<ICalculateFranchiseSeasonMetricsCommandHandler>(
                        h => h.ExecuteAsync(calculate, CancellationToken.None));
                }

                _logger.LogInformation(
                    "Single franchise season enrichment requested: record enrich enqueued, statistics {Statistics}, metrics {Metrics}.",
                    franchiseSeason.EspnRef is null ? "skipped (no ESPN ref)" : "requested",
                    _appMode.CurrentSport is Sport.FootballNcaa or Sport.FootballNfl ? "enqueued" : "n/a for sport");

                return new Success<Guid>(correlationId, ResultStatus.Accepted);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Single franchise season enrichment failed for {FranchiseSeasonId}", franchiseSeason.Id);
                return new Failure<Guid>(
                    default,
                    ResultStatus.Error,
                    [new ValidationFailure("Exception", ex.Message)]);
            }
        }
    }
}
