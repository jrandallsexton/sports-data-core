using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorLineScore)]
public class EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionCompetitorLineScoreDocumentProcessor(
        ILogger<EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus bus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, bus, externalRefIdentityGenerator, refs) { }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorLineScoreDto>();

        if (dto is null)
        {
            _logger.LogWarning("No line score found to process. Document was null after deserialization.");
            return;
        }

        _logger.LogDebug("Successfully deserialized LineScore DTO. Ref={Ref}, Period={Period}, Value={Value}, DisplayValue={DisplayValue}",
            dto.Ref,
            dto.Period,
            dto.Value,
            dto.DisplayValue);

        var competitionCompetitorId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionLineScoreRefToCompetitionCompetitorRef);

        if (competitionCompetitorId == null)
        {
            _logger.LogError("Unable to determine CompetitionCompetitorId from ParentId or URI");
            return; // fatal. do not retry
        }

        var competitionCompetitorIdValue = competitionCompetitorId.Value;

        _logger.LogDebug("Parsed CompetitionCompetitorId from ParentId. CompetitorId={CompetitorId}", competitionCompetitorIdValue);

        var exists = await _dataContext.CompetitionCompetitors
            .AsNoTracking()
            .AnyAsync(x => x.Id == competitionCompetitorIdValue);

        _logger.LogDebug("CompetitionCompetitor existence check. CompetitorId={CompetitorId}, Exists={Exists}", 
            competitionCompetitorIdValue, 
            exists);

        if (!exists)
        {
            var competitionCompetitorRef = EspnUriMapper.CompetitionLineScoreRefToCompetitionCompetitorRef(dto.Ref);
            var competitionCompetitorIdentity = _externalRefIdentityGenerator.Generate(competitionCompetitorRef);

            var competitionRef = EspnUriMapper.CompetitionLineScoreRefToCompetitionRef(dto.Ref);
            var competitionIdentity = _externalRefIdentityGenerator.Generate(competitionRef);

            await PublishDependencyRequest<Guid>(
                command,
                new EspnLinkDto { Ref = competitionCompetitorRef },
                parentId: competitionIdentity.CanonicalId,
                DocumentType.EventCompetitionCompetitor);

            throw new ExternalDocumentNotSourcedException($"No CompetitionCompetitor exists with ID: {competitionCompetitorIdValue}. Requested. Will retry.");
        }

        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);
        
        _logger.LogDebug("Generated identity for LineScore. CanonicalId={CanonicalId}, UrlHash={UrlHash}, CleanUrl={CleanUrl}",
            identity.CanonicalId,
            identity.UrlHash,
            identity.CleanUrl);

        var entry = await _dataContext.CompetitionCompetitorLineScores
            .AsTracking()
            .FirstOrDefaultAsync(x => x.Id == identity.CanonicalId);

        _logger.LogDebug("Database lookup for existing LineScore. CanonicalId={CanonicalId}, Found={Found}",
            identity.CanonicalId,
            entry is not null);

        if (entry is not null)
        {
            _logger.LogInformation("Updating existing CompetitorLineScore. Id={Id}, CompetitorId={CompetitorId}, Period={Period}, OldValue={OldValue}, NewValue={NewValue}", 
                entry.Id,
                competitionCompetitorIdValue, 
                dto.Period,
                entry.Value,
                dto.Value);

            entry.Value = dto.Value;
            entry.DisplayValue = dto.DisplayValue;
            entry.Period = dto.Period;
            entry.SourceId = dto.Source?.Id ?? string.Empty;
            entry.SourceDescription = dto.Source?.Description ?? string.Empty;
            entry.SourceState = dto.Source?.State;
            entry.ModifiedUtc = DateTime.UtcNow;
            entry.ModifiedBy = command.CorrelationId;

            _logger.LogDebug("Updated LineScore entity properties. Id={Id}, Value={Value}, DisplayValue={DisplayValue}, SourceId={SourceId}, SourceDescription={SourceDescription}",
                entry.Id,
                entry.Value,
                entry.DisplayValue,
                entry.SourceId,
                entry.SourceDescription);
        }
        else
        {
            _logger.LogInformation("Creating new CompetitorLineScore. CompetitorId={CompetitorId}, Period={Period}, Value={Value}, CanonicalId={CanonicalId}", 
                competitionCompetitorIdValue, 
                dto.Period,
                dto.Value,
                identity.CanonicalId);

            var entity = dto.AsEntity(
                competitionCompetitorIdValue,
                _externalRefIdentityGenerator,
                command.SourceDataProvider,
                command.CorrelationId);

            _logger.LogDebug("Created LineScore entity from DTO. Id={Id}, CompetitorId={CompetitorId}, Period={Period}, Value={Value}, DisplayValue={DisplayValue}, SourceId={SourceId}, SourceDescription={SourceDescription}, ExternalIdCount={ExternalIdCount}",
                entity.Id,
                entity.CompetitionCompetitorId,
                entity.Period,
                entity.Value,
                entity.DisplayValue,
                entity.SourceId,
                entity.SourceDescription,
                entity.ExternalIds.Count);

            await _dataContext.CompetitionCompetitorLineScores.AddAsync(entity);
            
            _logger.LogDebug("Added LineScore entity to DbContext. Id={Id}", entity.Id);
        }

        _logger.LogDebug("Saving changes to database...");
        
        var changesCount = await _dataContext.SaveChangesAsync();
        
        _logger.LogInformation("Persisted CompetitorLineScore. Id={Id}, CompetitorId={CompetitorId}, Period={Period}, Value={Value}, DisplayValue={DisplayValue}, ChangeCount={ChangeCount}", 
            identity.CanonicalId,
            competitionCompetitorIdValue, 
            dto.Period,
            dto.Value,
            dto.DisplayValue,
            changesCount);
        }
}