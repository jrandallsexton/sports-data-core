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

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorRecord)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetitionCompetitorRecord)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionCompetitorRecord)]
public class EventCompetitionCompetitorRecordDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public EventCompetitionCompetitorRecordDocumentProcessor(
        ILogger<EventCompetitionCompetitorRecordDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus eventBus,
        IGenerateExternalRefIdentities identityGenerator,
        IGenerateResourceRefs refs,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, eventBus, identityGenerator, refs)
    {
        _dateTimeProvider = dateTimeProvider;
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

                    // Jittered backoff before the next attempt. Seq analysis
                    // of the conflict bursts (single fan-out CorrelationId
                    // generating dozens of sibling messages within ~160ms)
                    // showed all 3 attempts firing within ~15ms total — the
                    // retry was spinning through the contention window. A
                    // 50-200ms jitter pushes the next attempt past the burst
                    // window, giving attempt 2/3 a real chance to land
                    // before escalating to outer-delivery retry.
                    var backoffMs = Random.Shared.Next(50, 200);
                    await Task.Delay(backoffMs);
                }
                catch (DbUpdateConcurrencyException)
                {
                    // Final retry failed. Previously this swallowed the
                    // exception with `return;`, which caused Hangfire /
                    // MassTransit to treat the message as succeeded and
                    // silently drop the update. Rethrow so the outer
                    // delivery layer can apply its own exponential backoff
                    // and retry — by the time the message redelivers, the
                    // burst window that caused the in-process contention
                    // will have cleared.
                    _logger.LogWarning(
                        "Concurrency conflict after {MaxRetries} retries. Rethrowing for outer-delivery retry. " +
                        "CompetitorId={CompetitorId}, Type={Type}, CorrelationId={CorrelationId}",
                        maxRetries,
                        competitorId,
                        dto.Type,
                        command.CorrelationId);
                    throw;
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
            CreatedUtc = _dateTimeProvider.UtcNow(),
            CreatedBy = command.CorrelationId
        };

        // Add stats
        CreateStatsForRecord(dto.Stats, record, command.CorrelationId, _dateTimeProvider.UtcNow());

        await _dataContext.CompetitionCompetitorRecords.AddAsync(record);

        _logger.LogInformation(
            "✅ RECORD_CREATED: CompetitionCompetitorRecord created. RecordId={RecordId}, Type={Type}, Stats={StatCount}",
            record.Id,
            dto.Type,
            record.Stats.Count);
    }

    private Task ProcessUpdate(
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
        existingRecord.ModifiedUtc = _dateTimeProvider.UtcNow();

        // Diff-merge stats by Name (ESPN's natural per-stat identifier).
        // The prior "RemoveRange + re-Add" pattern issued explicit DELETE
        // statements for every stat row; under burst-fan-out contention a
        // sibling worker could DELETE the same rows first, causing EF to
        // throw DbUpdateConcurrencyException on the row-count mismatch
        // (the entity has no IsRowVersion token, so EF only verifies
        // 1 row affected per DELETE). Diff-merge emits UPDATEs for the
        // steady-state case where ESPN ships the same stat keys with new
        // values — UPDATEs don't race the same way, and only orphan
        // removal / new additions hit DELETE / INSERT paths.
        var now = _dateTimeProvider.UtcNow();
        var incoming = (dto.Stats ?? new List<EspnRecordStatDto>())
            .Where(s => !string.IsNullOrEmpty(s.Name))
            .GroupBy(s => s.Name)
            .ToDictionary(g => g.Key, g => g.First());
        var existingByName = existingRecord.Stats
            .Where(s => !string.IsNullOrEmpty(s.Name))
            .GroupBy(s => s.Name)
            .ToDictionary(g => g.Key, g => g.First());

        // Update existing + insert new
        foreach (var (name, statDto) in incoming)
        {
            if (existingByName.TryGetValue(name, out var existingStat))
            {
                existingStat.DisplayName = statDto.DisplayName;
                existingStat.ShortDisplayName = statDto.ShortDisplayName;
                existingStat.Description = statDto.Description;
                existingStat.Abbreviation = statDto.Abbreviation;
                existingStat.Type = statDto.Type;
                existingStat.Value = statDto.Value;
                existingStat.DisplayValue = statDto.DisplayValue;
                existingStat.ModifiedUtc = now;
            }
            else
            {
                var newStat = new CompetitionCompetitorRecordStat
                {
                    Id = Guid.NewGuid(),
                    CompetitionCompetitorRecordId = existingRecord.Id,
                    Name = statDto.Name,
                    DisplayName = statDto.DisplayName,
                    ShortDisplayName = statDto.ShortDisplayName,
                    Description = statDto.Description,
                    Abbreviation = statDto.Abbreviation,
                    Type = statDto.Type,
                    Value = statDto.Value,
                    DisplayValue = statDto.DisplayValue,
                    CreatedUtc = now,
                    CreatedBy = command.CorrelationId
                };
                existingRecord.Stats.Add(newStat);
                // Mark on the DbSet too — relying on navigation-collection
                // auto-detection alone has surfaced as a real failure path
                // (InMemory provider raised "entity does not exist in the
                // store" on SaveChanges when only added to the collection).
                _dataContext.CompetitionCompetitorRecordStats.Add(newStat);
            }
        }

        // Remove orphans (stats ESPN no longer ships)
        var orphans = existingRecord.Stats
            .Where(s => !string.IsNullOrEmpty(s.Name) && !incoming.ContainsKey(s.Name))
            .ToList();
        foreach (var orphan in orphans)
        {
            existingRecord.Stats.Remove(orphan);
            _dataContext.CompetitionCompetitorRecordStats.Remove(orphan);
        }

        _logger.LogInformation(
            "✅ RECORD_UPDATED: CompetitionCompetitorRecord updated. RecordId={RecordId}, Stats={StatCount}",
            existingRecord.Id,
            existingRecord.Stats.Count);

        return Task.CompletedTask;
    }

    private static void CreateStatsForRecord(
        IEnumerable<EspnRecordStatDto>? statDtos,
        CompetitionCompetitorRecord record,
        Guid correlationId,
        DateTime now)
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
                CreatedUtc = now,
                CreatedBy = correlationId
            };

            record.Stats.Add(stat);
        }
    }
}
