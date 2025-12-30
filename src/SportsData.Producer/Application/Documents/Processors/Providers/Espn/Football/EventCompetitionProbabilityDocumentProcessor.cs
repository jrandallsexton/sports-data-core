using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionProbability)]
public class EventCompetitionProbabilityDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionProbabilityDocumentProcessor(
        ILogger<EventCompetitionProbabilityDocumentProcessor<TDataContext>> logger,
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
            _logger.LogInformation("EventCompetitionProbabilityDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}", 
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionProbabilityDocumentProcessor completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionProbabilityDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionProbabilityDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionProbabilityDto.");
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionProbabilityDto Ref is null or empty.");
            return;
        }

        var competitionId = await _dataContext.ResolveIdAsync<
            Competition, CompetitionExternalId>(
            dto.Competition,
            command.SourceDataProvider,
            () => _dataContext.Competitions,
            externalIdsNav: "ExternalIds",
            key: c => c.Id);


        if (competitionId is null || competitionId == Guid.Empty)
        {
            _logger.LogWarning("No matching competition found. Ref={Ref}", dto.Competition.Ref);
            return;
        }

        Guid? playId = null;

        if (!string.IsNullOrEmpty(dto.Play?.Ref?.ToString()))
        {
            playId = await _dataContext.ResolveIdAsync<
                CompetitionPlay, CompetitionPlayExternalId>(
                dto.Play,
                command.SourceDataProvider,
                () => _dataContext.CompetitionPlays,
                externalIdsNav: "ExternalIds",
                key: p => p.Id);
        }

        var newEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competitionId.Value,
            playId,
            command.CorrelationId);

        var lastSaved = await _dataContext.CompetitionProbabilities
            .Where(x => x.CompetitionId == competitionId.Value)
            .OrderByDescending(x => x.CreatedUtc)
            .FirstOrDefaultAsync();

        bool hasChanged = lastSaved is null ||
                          lastSaved.HomeWinPercentage != newEntity.HomeWinPercentage ||
                          lastSaved.AwayWinPercentage != newEntity.AwayWinPercentage ||
                          lastSaved.TiePercentage != newEntity.TiePercentage ||
                          lastSaved.SecondsLeft != newEntity.SecondsLeft;

        if (!hasChanged)
        {
            _logger.LogInformation("No probability change detected, skipping. CompetitionId={CompId}, Home={Home}%, Away={Away}%", 
                competitionId,
                newEntity.HomeWinPercentage,
                newEntity.AwayWinPercentage);
            return;
        }

        _logger.LogInformation("Win probability changed, publishing event. CompetitionId={CompId}, Home={Home}%, Away={Away}%, Tie={Tie}%, SecondsLeft={Seconds}", 
            competitionId,
            newEntity.HomeWinPercentage,
            newEntity.AwayWinPercentage,
            newEntity.TiePercentage,
            newEntity.SecondsLeft);

        await _publishEndpoint.Publish(new CompetitionWinProbabilityChanged(
            newEntity.CompetitionId,
            newEntity.PlayId,
            newEntity.HomeWinPercentage,
            newEntity.AwayWinPercentage,
            newEntity.TiePercentage,
            newEntity.SecondsLeft,
            DateTime.Parse(dto.LastModified).ToUniversalTime(),
            command.SourceDataProvider.ToString().ToLowerInvariant(),
            dto.Ref?.ToString() ?? string.Empty,
            dto.SequenceNumber,
            command.CorrelationId,
            CausationId.Producer.EventCompetitionProbabilityDocumentProcessor
        ));

        await _dataContext.CompetitionProbabilities.AddAsync(newEntity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionProbability snapshot. CompetitionId={CompId}, ProbabilityId={ProbId}, Sequence={Seq}", 
            competitionId,
            newEntity.Id,
            dto.SequenceNumber);
    }
}