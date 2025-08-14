using MassTransit;
using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
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
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public EventCompetitionPredictionDocumentProcessor(
        ILogger<EventCompetitionPredictionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IPublishEndpoint publishEndpoint,
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
            return; // terminal failure — don't retry
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionPredictorDto Ref is null or empty. {@Command}", command);
            return; // terminal failure — don't retry
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("ParentId is missing or invalid for CompetitionPrediction: {parentId}", command.ParentId);
            throw new InvalidOperationException("CompetitionId (ParentId) is required to process CompetitionPrediction");
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

        var knownMetrics = await _dataContext.PredictionMetrics
            .ToDictionaryAsync(x => x.Name.ToLower());

        var predictions = dto.AsEntities(
            _externalRefIdentityGenerator,
            competitionId,
            homeFranchiseSeasonId.Value,
            awayFranchiseSeasonId.Value,
            command.CorrelationId,
            knownMetrics);

        // Ensure new metrics are added to the DB
        var newMetrics = knownMetrics.Values
            .Where(m => !_dataContext.PredictionMetrics.Any(x => x.Id == m.Id))
            .ToList();

        if (newMetrics.Any())
            await _dataContext.PredictionMetrics.AddRangeAsync(newMetrics);

        // Remove existing CompetitionPrediction entries (clean insert pattern)
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

        // NOTE: Values are created by processor during AsEntities and tracked manually
        var predictionValues = predictions
            .SelectMany(p => new List<CompetitionPredictionValue>
            {
                // Not actually populated unless .AsEntity is updated to include nav
                // You may prefer to persist them inline instead of extracting here
            })
            .ToList();

        if (predictionValues.Any())
            await _dataContext.CompetitionPredictionValues.AddRangeAsync(predictionValues);

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionPredictions for competition {id}", competitionId);
    }
}