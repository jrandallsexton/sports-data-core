using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

/// <summary>
/// Processes CoachSeason documents from ESPN API, creating CoachSeason entities that link coaches to franchise seasons.
/// Example: http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/coaches/2331669
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.CoachSeason)]
public class CoachSeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public CoachSeasonDocumentProcessor(
        ILogger<CoachSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnCoachSeasonDto>();

        if (dto is null || dto.Ref is null)
        {
            _logger.LogError("Invalid or null EspnCoachSeasonDto.");
            return;
        }

        // Determine if this entity exists. Do NOT trust that it says it is a new document!
        var coachSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var entity = await _dataContext.CoachSeasons
            .FirstOrDefaultAsync(x => x.Id == coachSeasonIdentity.CanonicalId);

        if (entity is null)
        {
            _logger.LogInformation("Processing new CoachSeason entity. Ref={Ref}", dto.Ref);
            await ProcessNewEntity(command, dto, coachSeasonIdentity);
        }
        else
        {
            _logger.LogInformation("Processing CoachSeason update. CoachSeasonId={CoachSeasonId}, Ref={Ref}", 
                entity.Id, dto.Ref);
            await ProcessUpdate(command, dto, entity);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnCoachSeasonDto dto,
        ExternalRefIdentity coachSeasonIdentity)
    {
        _logger.LogInformation("Creating new CoachSeason.");

        // Preflight dependency check: Person (Coach) document must exist
        if (dto.Person?.Ref is null)
        {
            _logger.LogError("Person ref is null in CoachSeason DTO.");
            return;
        }

        var coachIdentity = _externalRefIdentityGenerator.Generate(dto.Person.Ref);
        var coachExists = await _dataContext.Coaches
            .AnyAsync(x => x.Id == coachIdentity.CanonicalId);

        if (!coachExists)
        {
            _logger.LogWarning("Coach not found. Requesting Person document sourcing. PersonRef={PersonRef}",
                dto.Person.Ref);

            await PublishChildDocumentRequest<string?>(
                command,
                dto.Person,
                parentId: null,
                DocumentType.Coach);

            throw new ExternalDocumentNotSourcedException(
                $"Coach not sourced yet for ref: {dto.Person.Ref}");
        }

        // Preflight dependency check: FranchiseSeason must exist
        if (dto.Team?.Ref is null)
        {
            _logger.LogError("Team ref is null in CoachSeason DTO.");
            return;
        }

        var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Team.Ref);
        var franchiseSeasonExists = await _dataContext.FranchiseSeasons
            .AnyAsync(x => x.Id == franchiseSeasonIdentity.CanonicalId);

        if (!franchiseSeasonExists)
        {
            await PublishChildDocumentRequest<string?>(
                command,
                dto.Team,
                parentId: null,
                DocumentType.TeamSeason);

            throw new ExternalDocumentNotSourcedException(
                $"FranchiseSeason not sourced yet for ref: {dto.Team.Ref}");
        }

        // Create new CoachSeason entity
        var newEntity = new CoachSeason
        {
            Id = coachSeasonIdentity.CanonicalId,
            CoachId = coachIdentity.CanonicalId,
            FranchiseSeasonId = franchiseSeasonIdentity.CanonicalId,
            Title = dto.LastName,
            IsActive = true,
            CreatedBy = command.CorrelationId,
            CreatedUtc = DateTime.UtcNow
        };

        await _dataContext.CoachSeasons.AddAsync(newEntity);

        _logger.LogInformation("Created new CoachSeason entity. CoachSeasonId={CoachSeasonId}", newEntity.Id);

        // Process child documents for new entity (will save changes at the end)
        await ProcessChildDocuments(command, dto, newEntity, isNew: true);
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnCoachSeasonDto dto,
        CoachSeason entity)
    {
        _logger.LogInformation("Updating CoachSeason. CoachSeasonId={CoachSeasonId}", entity.Id);

        entity.IsActive = true;
        entity.Title = dto.LastName; // Update title if changed
        entity.ModifiedUtc = DateTime.UtcNow;
        entity.ModifiedBy = command.CorrelationId;

        // Process child documents for update (will save changes at the end, respects ShouldSpawn)
        await ProcessChildDocuments(command, dto, entity, isNew: false);

        _logger.LogInformation("CoachSeason update completed. CoachSeasonId={CoachSeasonId}", entity.Id);
    }

    /// <summary>
    /// Processes child documents (CoachSeasonRecord) for a CoachSeason entity.
    /// For new entities (isNew=true), always spawns all child documents.
    /// For updates (isNew=false), respects ShouldSpawn filtering.
    /// </summary>
    private async Task ProcessChildDocuments(
        ProcessDocumentCommand command,
        EspnCoachSeasonDto dto,
        CoachSeason coachSeason,
        bool isNew)
    {
        _logger.LogInformation("Processing child documents for CoachSeason. CoachSeasonId={CoachSeasonId}, IsNew={IsNew}",
            coachSeason.Id, isNew);

        // CoachSeasonRecord documents - bypass ShouldSpawn for new entities, apply filtering for updates
        if (isNew || ShouldSpawn(DocumentType.CoachSeasonRecord, command))
        {
            if (dto.Records is { Count: > 0 })
            {
                _logger.LogInformation("Requesting {Count} CoachSeasonRecord documents. CoachSeasonId={CoachSeasonId}",
                    dto.Records.Count, coachSeason.Id);

                foreach (var recordRef in dto.Records)
                {
                    if (recordRef.Record is not null)
                    {
                        await PublishChildDocumentRequest(
                            command,
                            recordRef.Record,
                            parentId: coachSeason.Id,
                            DocumentType.CoachSeasonRecord);

                        _logger.LogDebug("Published DocumentRequested for CoachSeasonRecord: {RecordRef}",
                            recordRef.Record.Ref);
                    }
                }
            }
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Completed processing child documents for CoachSeason. CoachSeasonId={CoachSeasonId}",
            coachSeason.Id);
    }
}
