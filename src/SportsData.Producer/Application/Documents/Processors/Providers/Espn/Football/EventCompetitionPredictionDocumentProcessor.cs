using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/predictor?lang=en
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionPrediction)]
public class EventCompetitionPredictionDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionPredictionDocumentProcessor(
        ILogger<EventCompetitionPredictionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["DocumentType"] = command.DocumentType,
                   ["Season"] = command.Season ?? 0,
                   ["CompetitionId"] = command.ParentId ?? "Unknown"
               }))
        {
            _logger.LogInformation("EventCompetitionPredictionDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}", 
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionPredictionDocumentProcessor completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionPredictionDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionPredictorDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionPredictorDto.");
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionPredictorDto Ref is null or empty.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("ParentId is missing or invalid for CompetitionPrediction. ParentId={ParentId}", command.ParentId);
            return;
        }

        var homeFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            dto.HomeTeam.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        var awayFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            dto.AwayTeam.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        if (homeFranchiseSeasonId is null || awayFranchiseSeasonId is null)
        {
            _logger.LogWarning("FranchiseSeason resolution failed for predictor. Home={Home}, Away={Away}", 
                homeFranchiseSeasonId, 
                awayFranchiseSeasonId);
            return;
        }

        // 🔍 STEP 1: Extract all metric categories from both teams
        var allMetrics = dto.HomeTeam.Statistics
            .Concat(dto.AwayTeam.Statistics)
            .GroupBy(m => m.Name)
            .Select(g => g.First()) // de-dupe
            .ToList();

        // 🔍 STEP 2: Load existing metrics
        var existingMetrics = await _dataContext.PredictionMetrics
            .ToDictionaryAsync(x => x.Name);

        // 🔍 STEP 3: Identify new metrics
        var newMetrics = allMetrics
            .Where(m => !existingMetrics.ContainsKey(m.Name))
            .Select(m => new PredictionMetric
            {
                Id = Guid.NewGuid(),
                Name = m.Name,
                DisplayName = m.DisplayName,
                ShortDisplayName = m.ShortDisplayName,
                Abbreviation = m.Abbreviation,
                Description = m.Description
            })
            .ToList();

        if (newMetrics.Count > 0)
        {
            _logger.LogInformation("Discovered {Count} new prediction metrics. CompetitionId={CompId}, NewMetrics={Metrics}", 
                newMetrics.Count,
                competitionId,
                string.Join(", ", newMetrics.Select(m => m.Name)));
            
            await _dataContext.PredictionMetrics.AddRangeAsync(newMetrics);
            await _dataContext.SaveChangesAsync(); // save so they get their PKs
        }

        // 🔁 STEP 4: Rebuild dictionary with all metrics (including new)
        var allMetricsDict = await _dataContext.PredictionMetrics
            .ToDictionaryAsync(x => x.Name.ToLower());

        // 🏗 STEP 5: Build new CompetitionPrediction + Values
        var predictions = dto.AsEntities(
            _externalRefIdentityGenerator,
            competitionId,
            homeFranchiseSeasonId.Value,
            awayFranchiseSeasonId.Value,
            command.CorrelationId,
            allMetricsDict);

        // 🧹 STEP 6: Remove any existing predictions for this competition
        var existingPredictionIds = await _dataContext.CompetitionPredictions
            .Where(x => x.CompetitionId == competitionId)
            .Select(x => x.Id)
            .ToListAsync();

        if (existingPredictionIds.Any())
        {
            var toRemove = await _dataContext.CompetitionPredictionValues
                .Where(v => existingPredictionIds.Contains(v.CompetitionPredictionId))
                .ToListAsync();

            _logger.LogInformation("Removing existing predictions (hard replace). CompetitionId={CompId}, Predictions={PredictionCount}, Values={ValueCount}", 
                competitionId,
                existingPredictionIds.Count,
                toRemove.Count);

            _dataContext.CompetitionPredictionValues.RemoveRange(toRemove);

            var predictionsToRemove = await _dataContext.CompetitionPredictions
                .Where(x => existingPredictionIds.Contains(x.Id))
                .ToListAsync();

            _dataContext.CompetitionPredictions.RemoveRange(predictionsToRemove);
        }

        await _dataContext.CompetitionPredictions.AddRangeAsync(predictions);

        // ✅ STEP 7: Persist values (populated inside AsEntities)
        var predictionValues = predictions
            .SelectMany(p => p.Values)
            .ToList();

        if (predictionValues.Any())
            await _dataContext.CompetitionPredictionValues.AddRangeAsync(predictionValues);

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionPredictions. CompetitionId={CompId}, Predictions={PredictionCount}, Values={ValueCount}", 
            competitionId,
            predictions.Count,
            predictionValues.Count);
    }
}