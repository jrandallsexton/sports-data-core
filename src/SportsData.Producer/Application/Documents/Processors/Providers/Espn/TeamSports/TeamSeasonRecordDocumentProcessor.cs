using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonRecord)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.TeamSeasonRecord)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.TeamSeasonRecord)]
public class TeamSeasonRecordDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public TeamSeasonRecordDocumentProcessor(
        ILogger<TeamSeasonRecordDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var franchiseSeasonId = TryGetOrDeriveParentId(
            command, 
            EspnUriMapper.TeamSeasonRecordRefToTeamSeasonRef);

        if (franchiseSeasonId == null)
        {
            _logger.LogError("Unable to determine FranchiseSeasonId from ParentId or URI");
            return;
        }

        var franchiseSeasonIdValue = franchiseSeasonId.Value;

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == franchiseSeasonIdValue);

        if (franchiseSeason is null)
        {
            _logger.LogError("FranchiseSeason not found: {FranchiseSeasonId}", franchiseSeasonIdValue);
            return;
        }

        var dto = command.Document.FromJson<EspnTeamSeasonRecordDto>();

        if (dto is null)
        {
            _logger.LogError("DTO is null for TeamSeasonRecord processing. ParentId: {ParentId}", command.ParentId);
            return;
        }

        // Upsert by natural key (FranchiseSeasonId, Name, Type). True
        // in-place update — NOT delete/re-add — so the record keeps its
        // identity across re-sourcing, the write is a single atomic
        // SaveChanges, and a backfill over already-sourced seasons is a
        // quiet no-op wherever nothing actually changed.
        var existing = await _dataContext.FranchiseSeasonRecords
            .Include(r => r.Stats)
            .FirstOrDefaultAsync(r => r.FranchiseSeasonId == franchiseSeasonIdValue
                                   && r.Name == dto.Name
                                   && r.Type == dto.Type);

        if (existing is null)
        {
            var entity = dto.AsEntity(
                franchiseSeasonIdValue,
                franchiseSeason.FranchiseId,
                franchiseSeason.SeasonYear,
                command.CorrelationId);

            await _dataContext.FranchiseSeasonRecords.AddAsync(entity);

            await _publishEndpoint.Publish(new FranchiseSeasonRecordCreated(
                entity.AsCanonical(),
                null,
                command.Sport,
                franchiseSeason.SeasonYear,
                command.CorrelationId,
                command.MessageId));

            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                // At-least-once delivery + parallel workers: two deliveries
                // can both observe no existing row and both insert; the
                // loser lands here on the (FranchiseSeasonId, Name, Type)
                // unique index. Recover iff the winner's row exists.
                var recovered = await TryRecoverFromDuplicateInsertAsync(
                    () => _dataContext.FranchiseSeasonRecords
                        .AsNoTracking()
                        .AnyAsync(r => r.FranchiseSeasonId == franchiseSeasonIdValue
                                    && r.Name == dto.Name
                                    && r.Type == dto.Type),
                    $"TeamSeasonRecord '{dto.Name}' FranchiseSeasonId={franchiseSeasonIdValue}, CorrelationId={command.CorrelationId}");

                if (!recovered)
                {
                    throw;
                }
            }

            _logger.LogInformation(
                "Created TeamSeasonRecord '{RecordName}' for FranchiseSeason {Id}",
                dto.Name, franchiseSeasonIdValue);
            return;
        }

        if (RecordMatchesDto(existing, dto))
        {
            _logger.LogDebug(
                "TeamSeasonRecord '{RecordName}' unchanged for FranchiseSeason {Id} — skipping",
                dto.Name, franchiseSeasonIdValue);
            return;
        }

        existing.Abbreviation = dto.Abbreviation;
        existing.DisplayName = dto.DisplayName;
        existing.ShortDisplayName = dto.ShortDisplayName;
        existing.Description = dto.Description;
        existing.Summary = dto.Summary;
        existing.DisplayValue = dto.DisplayValue;
        existing.Value = dto.Value;
        existing.ModifiedUtc = _dateTimeProvider.UtcNow();
        existing.ModifiedBy = command.CorrelationId;

        // Stats are value children with no external identity: replace the
        // collection wholesale under the SAME parent id. Both states are
        // set EXPLICITLY — RemoveRange for the old rows, AddRange for the
        // new — because nav-fixup discovery marks client-keyed (set-Guid)
        // entities as Modified rather than Added, which then fails as an
        // update of rows that don't exist.
        var newStats = dto.Stats?.Select(st => st.AsEntity()).ToList() ?? [];
        foreach (var stat in newStats)
        {
            stat.FranchiseSeasonRecordId = existing.Id;
        }

        _dataContext.Set<Infrastructure.Data.Entities.FranchiseSeasonRecordStat>()
            .RemoveRange(existing.Stats);
        await _dataContext.Set<Infrastructure.Data.Entities.FranchiseSeasonRecordStat>()
            .AddRangeAsync(newStats);
        existing.Stats = newStats;

        await _publishEndpoint.Publish(new FranchiseSeasonRecordUpdated(
            existing.AsCanonical(),
            null,
            command.Sport,
            franchiseSeason.SeasonYear,
            command.CorrelationId,
            command.MessageId));

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Updated TeamSeasonRecord '{RecordName}' for FranchiseSeason {Id}",
            dto.Name, franchiseSeasonIdValue);
    }

    /// <summary>
    /// Content equality between the stored record and the sourced DTO —
    /// the fields we persist, plus the stat set by (Name, Value,
    /// DisplayValue). Identical re-sourcing (the common backfill case)
    /// becomes a no-op: no write, no event.
    /// </summary>
    private static bool RecordMatchesDto(
        Infrastructure.Data.Entities.FranchiseSeasonRecord existing,
        EspnTeamSeasonRecordDto dto)
    {
        if (existing.Summary != dto.Summary
            || existing.DisplayValue != dto.DisplayValue
            || !existing.Value.Equals(dto.Value)
            || existing.Abbreviation != dto.Abbreviation
            || existing.DisplayName != dto.DisplayName
            || existing.ShortDisplayName != dto.ShortDisplayName
            || existing.Description != dto.Description)
        {
            return false;
        }

        var dtoStats = dto.Stats ?? [];
        if (existing.Stats.Count != dtoStats.Count)
            return false;

        // Multiset comparison — ESPN can emit duplicate stat tuples, and a
        // set-based check would treat {A, A, B} as equal to {A, B, B}.
        var existingCounts = existing.Stats
            .GroupBy(st => (st.Name, st.Value, st.DisplayValue))
            .ToDictionary(g => g.Key, g => g.Count());

        return dtoStats
            .GroupBy(st => (st.Name, st.Value, st.DisplayValue))
            .All(g => existingCounts.TryGetValue(g.Key, out var count) && count == g.Count());
    }
}
