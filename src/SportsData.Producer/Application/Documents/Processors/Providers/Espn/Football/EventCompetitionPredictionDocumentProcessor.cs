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
public class EventCompetitionPredictionDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<EventCompetitionPredictionDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public EventCompetitionPredictionDocumentProcessor(
        ILogger<EventCompetitionPredictionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Began with {@command}", command);

            try
            {
                await ProcessInternal(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionPredictorDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnEventCompetitionPredictorDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionPredictorDto Ref is null or empty. {@Command}", command);
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("ParentId is missing or invalid for CompetitionPrediction: {parentId}", command.ParentId);
            return;
        }

        var homeFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.HomeTeam.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
            _logger);

        var awayFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.AwayTeam.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
            _logger);

        if (homeFranchiseSeasonId is null || awayFranchiseSeasonId is null)
        {
            _logger.LogWarning("FranchiseSeason resolution failed for predictor. Home: {home}, Away: {away}", homeFranchiseSeasonId, awayFranchiseSeasonId);
            return;
        }

        // 🔍 STEP 1: Extract all metric categories from both teams
        var allMetrics = dto.HomeTeam.Statistics
            .Concat(dto.AwayTeam.Statistics)
            .GroupBy(m => m.Name.ToLowerInvariant())
            .Select(g => g.First()) // de-dupe
            .ToList();

        // 🔍 STEP 2: Load existing metrics
        var existingMetrics = await _dataContext.PredictionMetrics
            .ToDictionaryAsync(x => x.Name.ToLower());

        // 🔍 STEP 3: Identify new metrics
        var newMetrics = allMetrics
            .Where(m => !existingMetrics.ContainsKey(m.Name.ToLowerInvariant()))
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

        if (newMetrics.Any())
        {
            _logger.LogInformation("Discovered {count} new prediction metrics.", newMetrics.Count);
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

        _logger.LogInformation("Persisted CompetitionPredictions and {count} values for competition {id}", predictionValues.Count, competitionId);
    }

}