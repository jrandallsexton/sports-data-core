using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Coach)]
public class CoachDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public CoachDocumentProcessor(
        ILogger<CoachDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            _logger.LogInformation("Began processing Coach with {@Command}", command);
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
        var dto = command.Document.FromJson<EspnCoachDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnCoachDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnCoachDto Ref is null or empty. {@Command}", command);
            return;
        }

        var urlHash = HashProvider.GenerateHashFromUri(dto.Ref);
        var coach = await _dataContext.Coaches
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.ExternalIds.Any(e => e.Value == urlHash && e.Provider == command.SourceDataProvider));

        if (coach is null)
        {
            await ProcessNewEntity(command, dto);
        }
        else
        {
            await ProcessUpdate(command, dto, coach);
        }

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessNewEntity(ProcessDocumentCommand command, EspnCoachDto dto)
    {
        var newEntity = dto.AsEntity(_externalRefIdentityGenerator, command.CorrelationId);

        await _dataContext.Coaches.AddAsync(newEntity);

        _logger.LogInformation("Created new Coach entity: {CoachId}", newEntity.Id);

        // Process child documents for new entity (will save changes at the end)
        await ProcessChildDocuments(command, dto, newEntity, isNew: true);
    }

    private async Task ProcessUpdate(ProcessDocumentCommand command, EspnCoachDto dto, Coach coach)
    {
        var updated = false;
        
        if (coach.FirstName != dto.FirstName)
        {
            coach.FirstName = dto.FirstName;
            updated = true;
        }
        
        if (coach.LastName != dto.LastName)
        {
            coach.LastName = dto.LastName;
            updated = true;
        }
        
        if (coach.DateOfBirth != dto.DateOfBirth)
        {
            coach.DateOfBirth = dto.DateOfBirth;
            updated = true;
        }
        
        if (coach.Experience != dto.Experience)
        {
            coach.Experience = dto.Experience;
            updated = true;
        }
        
        if (updated)
        {
            coach.ModifiedUtc = DateTime.UtcNow;
            coach.ModifiedBy = command.CorrelationId;
            _logger.LogInformation("Updated Coach entity: {CoachId}", coach.Id);
        }
        else
        {
            _logger.LogInformation("No changes detected for Coach {CoachId}", coach.Id);
        }

        // Process child documents for update (will save changes at the end, respects ShouldSpawn)
        await ProcessChildDocuments(command, dto, coach, isNew: false);
    }

    /// <summary>
    /// Processes child documents (CoachRecord, CoachSeason) for a Coach entity.
    /// For new entities (isNew=true), always spawns all child documents.
    /// For updates (isNew=false), respects ShouldSpawn filtering.
    /// </summary>
    private async Task ProcessChildDocuments(
        ProcessDocumentCommand command,
        EspnCoachDto dto,
        Coach coach,
        bool isNew)
    {
        _logger.LogInformation("Processing child documents for Coach. CoachId={CoachId}, IsNew={IsNew}",
            coach.Id, isNew);

        // CoachRecord documents - bypass ShouldSpawn for new entities, apply filtering for updates
        if (isNew || ShouldSpawn(DocumentType.CoachRecord, command))
        {
            if (dto.CareerRecords is { Count: > 0 })
            {
                _logger.LogInformation("Requesting {Count} CoachRecord documents. CoachId={CoachId}",
                    dto.CareerRecords.Count, coach.Id);

                foreach (var recordDto in dto.CareerRecords)
                {
                    await PublishChildDocumentRequest(
                        command,
                        recordDto,
                        coach.Id,
                        DocumentType.CoachRecord,
                        CausationId.Producer.CoachDocumentProcessor);

                    _logger.LogDebug("Published DocumentRequested for CoachRecord: {RecordRef}",
                        recordDto.Ref);
                }
            }
        }

        // CoachSeason documents - bypass ShouldSpawn for new entities, apply filtering for updates
        if (isNew || ShouldSpawn(DocumentType.CoachSeason, command))
        {
            if (dto.CoachSeasons is { Count: > 0 })
            {
                _logger.LogInformation("Requesting {Count} CoachSeason documents. CoachId={CoachId}",
                    dto.CoachSeasons.Count, coach.Id);

                foreach (var seasonDto in dto.CoachSeasons)
                {
                    await PublishChildDocumentRequest(
                        command,
                        seasonDto,
                        coach.Id,
                        DocumentType.CoachSeason,
                        CausationId.Producer.CoachDocumentProcessor);

                    _logger.LogDebug("Published DocumentRequested for CoachSeason: {SeasonRef}",
                        seasonDto.Ref);
                }
            }
        }

        _logger.LogInformation("Completed processing child documents for Coach. CoachId={CoachId}",
            coach.Id);
    }
}
