using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionSituation)]
public class EventCompetitionSituationDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly DocumentProcessingConfig _config;

    public EventCompetitionSituationDocumentProcessor(
        ILogger<EventCompetitionSituationDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus bus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        DocumentProcessingConfig config)
        : base(logger, dataContext, bus, externalRefIdentityGenerator, refs)
    {
        _config = config;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionSituationDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionSituationDto.");
            return;
        }

        if (dto is { Down: 0, Distance: 0, YardLine: 0 })
        {
            _logger.LogInformation("Situation has no data (down, distance, yardLine all zero), skipping.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("ParentId must be a valid Guid for CompetitionId. ParentId={ParentId}", command.ParentId);
            return;
        }

        Guid? lastPlayId = null;

        if (dto.LastPlay is not null && !string.IsNullOrEmpty(dto.LastPlay.Ref.OriginalString))
        {
            var lastPlayIdentity = _externalRefIdentityGenerator.Generate(dto.LastPlay.Ref);

            var lastPlay = await _dataContext.CompetitionPlays
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == lastPlayIdentity.CanonicalId);

            if (lastPlay == null)
            {
                if (!_config.EnableDependencyRequests)
                {
                    throw new ExternalDocumentNotSourcedException(
                        $"Play {dto.LastPlay.Ref} not found. Will retry when available.");
                }
                else
                {
                    _logger.LogWarning("LastPlay not found, raising DocumentRequested. PlayRef={PlayRef}", dto.LastPlay.Ref);
                    
                    await PublishChildDocumentRequest(
                        command,
                        dto.LastPlay,
                        competitionId,
                        DocumentType.EventCompetitionPlay);
                    
                    await _dataContext.SaveChangesAsync();

                    throw new ExternalDocumentNotSourcedException($"Play {dto.LastPlay.Ref} not found. Will retry.");
                }
            }

            lastPlayId = lastPlay.Id;
        }

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competitionId,
            lastPlayId,
            command.CorrelationId);

        var exists = await _dataContext.CompetitionSituations
            .AsNoTracking()
            .AnyAsync(x => x.Id == entity.Id);

        if (exists)
        {
            _logger.LogInformation("CompetitionSituation already exists, skipping. SituationId={Id}", entity.Id);
            return;
        }

        _logger.LogInformation("Creating new CompetitionSituation. CompetitionId={CompId}, Down={Down}, Distance={Distance}, YardLine={YardLine}", 
            competitionId,
            dto.Down,
            dto.Distance,
            dto.YardLine);

        await _dataContext.CompetitionSituations.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionSituation. CompetitionId={CompId}, SituationId={SituationId}", 
            competitionId,
            entity.Id);
    }
}