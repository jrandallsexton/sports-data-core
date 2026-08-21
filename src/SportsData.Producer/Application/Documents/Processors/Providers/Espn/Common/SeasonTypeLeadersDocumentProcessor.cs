using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

/// <summary>
/// League-wide season stat leaders (the document behind ESPN's Season
/// Leaders UI). Scoped by season TYPE — types/2 (regular season) and
/// types/3 (through postseason) are verified DISTINCT datasets, so both are
/// sourced and stored with their type code. Rows replace WHOLESALE per
/// (SeasonYear, SeasonTypeCode): ranks shuffle every game week and per-row
/// identity has no value. Leaders resolve to AthleteSeason/FranchiseSeason
/// via the document's season-scoped refs in BATCH (one query per id kind,
/// not one per row — 13 categories × 200 leaders would otherwise be ~5k
/// queries). Unresolvable athletes are skipped with a logged count: the
/// document carries refs, not names, so an unresolved row has nothing for a
/// consumer to join or display.
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonTypeLeaders)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.SeasonTypeLeaders)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.SeasonTypeLeaders)]
public class SeasonTypeLeadersDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public SeasonTypeLeadersDocumentProcessor(
        ILogger<SeasonTypeLeadersDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnSeasonTypeLeadersDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnSeasonTypeLeadersDto. {@Command}", command);
            return;
        }

        if (dto.Ref is null)
        {
            _logger.LogError("SeasonTypeLeaders DTO Ref is null. {@Command}", command);
            return;
        }

        if (!EspnUriMapper.TryExtractSeasonYear(dto.Ref, out var seasonYear))
        {
            if (command.SeasonYear is null)
            {
                _logger.LogError("SeasonYear not derivable from ref or command. Ref={Ref}", dto.Ref);
                return;
            }
            seasonYear = command.SeasonYear.Value;
        }

        if (!EspnUriMapper.TryExtractSeasonType(dto.Ref, out var seasonTypeCode))
        {
            _logger.LogError(
                "Season TYPE not present in leaders ref — the type distinguishes regular-season " +
                "from through-postseason leaderboards and must come from the URL. Ref={Ref}",
                dto.Ref);
            return;
        }

        var categories = dto.Categories ?? [];
        var leaderRows = categories
            .Where(c => c.Leaders is { Count: > 0 })
            .SelectMany(c => c.Leaders.Select((l, i) => (Category: c, Leader: l, Rank: i + 1)))
            .Where(x => x.Leader.Athlete?.Ref is not null)
            .ToList();

        // Batch ref → id resolution: one dictionary per id kind, keyed by the
        // clean-URL hash of the season-scoped refs the document carries.
        var athleteHashByRef = leaderRows
            .Select(x => x.Leader.Athlete!.Ref!)
            .Distinct()
            .ToDictionary(r => r, r => _externalRefIdentityGenerator.Generate(r).UrlHash);

        var teamHashByRef = leaderRows
            .Where(x => x.Leader.Team?.Ref is not null)
            .Select(x => x.Leader.Team!.Ref!)
            .Distinct()
            .ToDictionary(r => r, r => _externalRefIdentityGenerator.Generate(r).UrlHash);

        var athleteHashes = athleteHashByRef.Values.ToList();
        var athleteSeasonIdByHash = await _dataContext.AthleteSeasonExternalIds
            .AsNoTracking()
            .Where(x => athleteHashes.Contains(x.SourceUrlHash))
            .Select(x => new { x.SourceUrlHash, x.AthleteSeasonId })
            .ToDictionaryAsync(x => x.SourceUrlHash, x => x.AthleteSeasonId);

        var teamHashes = teamHashByRef.Values.ToList();
        var franchiseSeasonIdByHash = await _dataContext.FranchiseSeasonExternalIds
            .AsNoTracking()
            .Where(x => teamHashes.Contains(x.SourceUrlHash))
            .Select(x => new { x.SourceUrlHash, x.FranchiseSeasonId })
            .ToDictionaryAsync(x => x.SourceUrlHash, x => x.FranchiseSeasonId);

        var entities = new List<SeasonTypeLeader>(leaderRows.Count);
        var unresolvedAthletes = 0;

        foreach (var (category, leader, rank) in leaderRows)
        {
            if (!athleteSeasonIdByHash.TryGetValue(athleteHashByRef[leader.Athlete!.Ref!], out var athleteSeasonId))
            {
                unresolvedAthletes++;
                continue;
            }

            Guid? franchiseSeasonId = null;
            if (leader.Team?.Ref is not null &&
                franchiseSeasonIdByHash.TryGetValue(teamHashByRef[leader.Team.Ref], out var fsId))
            {
                franchiseSeasonId = fsId;
            }

            entities.Add(new SeasonTypeLeader
            {
                Id = Guid.NewGuid(),
                SeasonYear = seasonYear,
                SeasonTypeCode = seasonTypeCode,
                CategoryName = category.Name,
                CategoryDisplayName = category.DisplayName,
                Rank = rank,
                Value = leader.Value,
                DisplayValue = leader.DisplayValue,
                AthleteSeasonId = athleteSeasonId,
                FranchiseSeasonId = franchiseSeasonId,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = command.CorrelationId
            });
        }

        // Wholesale replace for this (SeasonYear, SeasonTypeCode) leaderboard.
        var existing = await _dataContext.SeasonTypeLeaders
            .Where(x => x.SeasonYear == seasonYear && x.SeasonTypeCode == seasonTypeCode)
            .ToListAsync();

        if (existing.Count > 0)
            _dataContext.SeasonTypeLeaders.RemoveRange(existing);

        await _dataContext.SeasonTypeLeaders.AddRangeAsync(entities);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "SeasonTypeLeaders processed. SeasonYear={SeasonYear}, SeasonTypeCode={SeasonTypeCode}, " +
            "Categories={Categories}, RowsWritten={RowsWritten}, Replaced={Replaced}, UnresolvedAthletes={UnresolvedAthletes}",
            seasonYear, seasonTypeCode, categories.Count, entities.Count, existing.Count, unresolvedAthletes);
    }
}
