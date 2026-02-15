using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorRecord)]
public class EventCompetitionCompetitorRecordDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionCompetitorRecordDocumentProcessor(
        ILogger<EventCompetitionCompetitorRecordDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities identityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, eventBus, identityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorRecordDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionCompetitorRecordDto.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitorId))
        {
            _logger.LogError("Invalid or missing CompetitionCompetitor ID in ParentId.");
            return;
        }

        _logger.LogInformation(
            "📊 RECORD_PROCESSING: Type={RecordType}, Summary={Summary}, CompetitionCompetitorId={CompetitorId}",
            dto.Type,
            dto.Summary,
            competitorId);

        // Check if competitor exists
        var competitorExists = await _dataContext.CompetitionCompetitors
            .AnyAsync(c => c.Id == competitorId);

        if (!competitorExists)
        {
            _logger.LogError("CompetitionCompetitor with Id {CompetitorId} not found", competitorId);
            return;
        }

        // Check if record already exists for this competitor and type
        var existingRecord = await _dataContext.CompetitionCompetitorRecords
            .Include(r => r.Stats)
            .FirstOrDefaultAsync(r =>
                r.CompetitionCompetitorId == competitorId &&
                r.Type == dto.Type);

        if (existingRecord != null)
        {
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                await ProcessUpdate(command, dto, existingRecord);
                
                try
                {
                    await _dataContext.SaveChangesAsync();
                    break; // Success - exit retry loop
                }
                catch (DbUpdateConcurrencyException) when (attempt < maxRetries - 1)
                {
                    // Another process updated - reload and retry
                    _logger.LogWarning(
                        "Concurrency conflict updating CompetitionCompetitorRecord (attempt {Attempt}/{MaxRetries}). " +
                        "CompetitorId={CompetitorId}, Type={Type}",
                        attempt + 1,
                        maxRetries,
                        competitorId,
                        dto.Type);
                    
                    // Detach tracked stats marked as Deleted
                    var trackedStats = _dataContext.ChangeTracker.Entries<CompetitionCompetitorRecordStat>()
                        .Where(e => e.Entity.CompetitionCompetitorRecordId == existingRecord.Id)
                        .ToList();
                    
                    foreach (var stat in trackedStats)
                    {
                        stat.State = EntityState.Detached;
                    }
                    
                    // Detach stale parent entity
                    _dataContext.Entry(existingRecord).State = EntityState.Detached;
                    
                    // Reload fresh record
                    existingRecord = await _dataContext.CompetitionCompetitorRecords
                        .Include(r => r.Stats)
                        .FirstOrDefaultAsync(r =>
                            r.CompetitionCompetitorId == competitorId &&
                            r.Type == dto.Type);
                    
                    if (existingRecord == null)
                    {
                        _logger.LogWarning(
                            "CompetitionCompetitorRecord deleted during retry. " +
                            "CompetitorId={CompetitorId}, Type={Type}, CorrelationId={CorrelationId}",
                            competitorId,
                            dto.Type,
                            command.CorrelationId);
                        return;
                    }
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Final retry failed - another pod won
                    _logger.LogWarning(
                        "Concurrency conflict after {MaxRetries} retries. Another process owns this update. " +
                        "CompetitorId={CompetitorId}, Type={Type}, CorrelationId={CorrelationId}",
                        maxRetries,
                        competitorId,
                        dto.Type,
                        command.CorrelationId);
                    return;
                }
            }
        }
        else
        {
            try
            {
                await ProcessNewEntity(command, dto, competitorId);
                await _dataContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex)
            {
                // Handle race condition: another thread may have inserted between our query and insert
                // Check if the inner exception indicates a unique constraint violation
                if (ex.InnerException?.Message?.Contains("duplicate key") == true ||
                    ex.InnerException?.Message?.Contains("unique constraint") == true)
                {
                    _logger.LogWarning(
                        "Race condition detected inserting CompetitionCompetitorRecord. Retrying as update. CompetitorId={CompetitorId}, Type={Type}",
                        competitorId,
                        dto.Type);

                    // Reload the existing record (inserted by another thread)
                    existingRecord = await _dataContext.CompetitionCompetitorRecords
                        .Include(r => r.Stats)
                        .FirstOrDefaultAsync(r =>
                            r.CompetitionCompetitorId == competitorId &&
                            r.Type == dto.Type);

                    if (existingRecord != null)
                    {
                        await ProcessUpdate(command, dto, existingRecord);
                        await _dataContext.SaveChangesAsync();
                    }
                    else
                    {
                        // Record still doesn't exist - rethrow original exception
                        throw;
                    }
                }
                else
                {
                    // Not a unique constraint violation - rethrow
                    throw;
                }
            }
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorRecordDto dto,
        Guid competitorId)
    {
        _logger.LogInformation(
            "🆕 NEW_RECORD: Creating new CompetitionCompetitorRecord. Type={Type}, Summary={Summary}",
            dto.Type,
            dto.Summary);

        var record = new CompetitionCompetitorRecord
        {
            Id = Guid.NewGuid(),
            CompetitionCompetitorId = competitorId,
            Type = dto.Type,
            Name = dto.Name,
            Summary = dto.Summary,
            DisplayValue = dto.DisplayValue,
            Value = dto.Value,
            CreatedUtc = DateTime.UtcNow,
            CreatedBy = command.CorrelationId
        };

        // Add stats
        CreateStatsForRecord(dto.Stats, record, command.CorrelationId);

        await _dataContext.CompetitionCompetitorRecords.AddAsync(record);

        _logger.LogInformation(
            "✅ RECORD_CREATED: CompetitionCompetitorRecord created. RecordId={RecordId}, Type={Type}, Stats={StatCount}",
            record.Id,
            dto.Type,
            record.Stats.Count);
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorRecordDto dto,
        CompetitionCompetitorRecord existingRecord)
    {
        _logger.LogInformation(
            "🔄 UPDATE_RECORD: Updating existing CompetitionCompetitorRecord. RecordId={RecordId}, Type={Type}",
            existingRecord.Id,
            dto.Type);

        // Update record properties
        existingRecord.Name = dto.Name;
        existingRecord.Summary = dto.Summary;
        existingRecord.DisplayValue = dto.DisplayValue;
        existingRecord.Value = dto.Value;
        existingRecord.ModifiedUtc = DateTime.UtcNow;

        // Remove old stats
        _dataContext.CompetitionCompetitorRecordStats.RemoveRange(existingRecord.Stats);
        existingRecord.Stats.Clear();

        // Add new stats
        CreateStatsForRecord(dto.Stats, existingRecord, command.CorrelationId);

        _logger.LogInformation(
            "✅ RECORD_UPDATED: CompetitionCompetitorRecord updated. RecordId={RecordId}, Stats={StatCount}",
            existingRecord.Id,
            existingRecord.Stats.Count);
    }

    private static void CreateStatsForRecord(
        IEnumerable<EspnRecordStatDto>? statDtos,
        CompetitionCompetitorRecord record,
        Guid correlationId)
    {
        if (statDtos == null)
            return;

        foreach (var statDto in statDtos)
        {
            var stat = new CompetitionCompetitorRecordStat
            {
                Id = Guid.NewGuid(),
                CompetitionCompetitorRecordId = record.Id,
                Name = statDto.Name,
                DisplayName = statDto.DisplayName,
                ShortDisplayName = statDto.ShortDisplayName,
                Description = statDto.Description,
                Abbreviation = statDto.Abbreviation,
                Type = statDto.Type,
                Value = statDto.Value,
                DisplayValue = statDto.DisplayValue,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = correlationId
            };

            record.Stats.Add(stat);
        }
    }
}
