using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

/// <summary>
/// Processes team season leader statistics.
/// Example: http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/seasons/2025/types/3/teams/99/leaders
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonLeaders)]
public class TeamSeasonLeadersDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{

    public TeamSeasonLeadersDocumentProcessor(
        ILogger<TeamSeasonLeadersDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnLeadersDto>();
        if (dto is null || string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("Invalid or null EspnLeadersDto.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var franchiseSeasonId))
        {
            _logger.LogError("Invalid or missing ParentId. ParentId={ParentId}", command.ParentId);
            return;
        }

        var franchiseSeason = await _dataContext.FranchiseSeasons
            .Include(x => x.ExternalIds)
            .Include(x => x.Leaders)
            .ThenInclude(x => x.Stats)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == franchiseSeasonId);

        if (franchiseSeason is null)
        {
            throw new ExternalDocumentNotSourcedException(
                $"FranchiseSeason {franchiseSeasonId} not found. Will retry when available.");
        }

        // Preflight dependency resolution - fail fast before deleting existing data
        var athleteSeasonCache = new Dictionary<string, Guid>();
        var missingAthleteSeasonRefs = new HashSet<Uri>();

        if (dto.Categories != null)
        {
            foreach (var category in dto.Categories)
            {
                if (category == null)
                {
                    _logger.LogWarning("Encountered null category, skipping.");
                    continue;
                }

                if (category.Leaders == null)
                {
                    _logger.LogDebug("Category has null Leaders collection, skipping. Category={CategoryName}", category.Name);
                    continue;
                }

                foreach (var leaderDto in category.Leaders)
                {
                    if (leaderDto == null)
                    {
                        _logger.LogWarning("Encountered null leader in category, skipping. Category={CategoryName}", category.Name);
                        continue;
                    }

                    if (leaderDto.Athlete?.Ref == null)
                    {
                        _logger.LogDebug("Leader has null Athlete or Ref, skipping. Category={CategoryName}", category.Name);
                        continue;
                    }

                    if (leaderDto.Statistics?.Ref == null)
                        continue;

                    // Collect missing athlete seasons instead of throwing immediately
                    var athleteSeasonId = await ResolveAthleteSeasonIdAsync(leaderDto.Athlete, command, athleteSeasonCache);
                    if (athleteSeasonId == null)
                    {
                        missingAthleteSeasonRefs.Add(leaderDto.Athlete.Ref);
                    }
                }
            }
        }

        // Batch-publish all missing athlete-season dependencies
        if (missingAthleteSeasonRefs.Count > 0)
        {
            foreach (var athleteSeasonRef in missingAthleteSeasonRefs)
            {
                var athleteRef = EspnUriMapper.AthleteSeasonToAthleteRef(athleteSeasonRef);
                var athleteIdentity = _externalRefIdentityGenerator.Generate(athleteRef);

                await PublishChildDocumentRequest(
                    command,
                    new EspnLinkDto { Ref = athleteSeasonRef },
                    athleteIdentity.CanonicalId.ToString(),
                    DocumentType.AthleteSeason);
            }

            throw new ExternalDocumentNotSourcedException(
                $"Missing {missingAthleteSeasonRefs.Count} AthleteSeason document(s). Requested dependencies will be processed and retried.");
        }

        // All dependencies resolved - safe to proceed with wholesale replacement
        var existingStatsCount = franchiseSeason.Leaders.Sum(l => l.Stats.Count);
        var existingLeadersCount = franchiseSeason.Leaders.Count;
        var isNew = existingLeadersCount == 0;

        _dataContext.FranchiseSeasonLeaderStats.RemoveRange(
            franchiseSeason.Leaders.SelectMany(l => l.Stats));
        _dataContext.FranchiseSeasonLeaders.RemoveRange(franchiseSeason.Leaders);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Removed existing leaders. FranchiseSeasonId={FranchiseSeasonId}, Leaders={LeaderCount}, Stats={StatCount}, IsNew={IsNew}",
            franchiseSeasonId,
            existingLeadersCount,
            existingStatsCount,
            isNew);

        var leaders = new List<FranchiseSeasonLeader>();

        if (dto.Categories != null)
        {
            foreach (var category in dto.Categories)
            {
                if (category == null)
                {
                    _logger.LogWarning("Encountered null category during processing, skipping.");
                    continue;
                }

                if (category.Leaders == null)
                {
                    _logger.LogDebug("Category has null Leaders collection during processing, skipping. Category={CategoryName}", category.Name);
                    continue;
                }
                var leaderCategory = await _dataContext.LeaderCategories
                    .FirstOrDefaultAsync(x => x.Name == category.Name);

                if (leaderCategory == null)
                {
                    _logger.LogInformation("Leader category not found, creating new category. Category={Category}, DisplayName={DisplayName}",
                        category.Name,
                        category.DisplayName);

                    // Get next available Id (ValueGeneratedNever requires explicit Id)
                    var maxId = await _dataContext.LeaderCategories
                        .AsNoTracking()
                        .MaxAsync(x => (int?)x.Id) ?? 0;

                    leaderCategory = new CompetitionLeaderCategory
                    {
                        Id = maxId + 1,
                        Name = category.Name,
                        DisplayName = category.DisplayName,
                        ShortDisplayName = category.ShortDisplayName ?? category.DisplayName,
                        Abbreviation = category.Abbreviation ?? category.DisplayName,
                        CreatedUtc = DateTime.UtcNow,
                        CreatedBy = command.CorrelationId
                    };

                    _dataContext.LeaderCategories.Add(leaderCategory);

                    try
                    {
                        await _dataContext.SaveChangesAsync();
                    }
                    catch (DbUpdateException)
                    {
                        // Race condition: another processor created this category concurrently
                        // Discard our failed attempt and retrieve the existing category
                        _logger.LogDebug("Concurrent category creation detected, retrieving existing category. Category={Category}", category.Name);

                        // Remove the failed entity from change tracker
                        _dataContext.Entry(leaderCategory).State = EntityState.Detached;

                        // Reload the existing category
                        leaderCategory = await _dataContext.LeaderCategories
                            .AsNoTracking()
                            .FirstOrDefaultAsync(x => x.Name == category.Name);

                        if (leaderCategory == null)
                        {
                            // Shouldn't happen, but log and rethrow if it does
                            _logger.LogError("Failed to retrieve category after concurrent creation. Category={Category}", category.Name);
                            throw;
                        }
                    }
                }

                var leaderEntity = FranchiseSeasonLeaderExtensions.AsEntity(
                    category,
                    franchiseSeasonId,
                    leaderCategory.Id,
                    command.CorrelationId
                );

                foreach (var leaderDto in category.Leaders)
                {
                    if (leaderDto == null)
                    {
                        _logger.LogWarning("Encountered null leader during processing, skipping. Category={CategoryName}", category.Name);
                        continue;
                    }

                    if (leaderDto.Athlete?.Ref == null)
                    {
                        _logger.LogDebug("Leader has null Athlete or Ref during processing, skipping. Category={CategoryName}", category.Name);
                        continue;
                    }

                    if (leaderDto.Statistics?.Ref == null)
                    {
                        _logger.LogDebug("Leader statistics ref is null, skipping. Category={CategoryName}", category.Name);
                        continue;
                    }

                    var athleteSeasonId = await ResolveAthleteSeasonIdAsync(leaderDto.Athlete, command, athleteSeasonCache);

                    // athleteSeasonId should never be null here since preflight already validated all dependencies
                    if (athleteSeasonId == null)
                    {
                        _logger.LogError("AthleteSeasonId is null after preflight validation. This should not happen. Ref={Ref}", leaderDto.Athlete.Ref);
                        continue;
                    }

                    var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(leaderDto.Athlete.Ref);

                    // For new leaders, always spawn child documents; for updates, respect ShouldSpawn filtering
                    if (isNew || ShouldSpawn(DocumentType.AthleteSeasonStatistics, command))
                    {
                        // Spawn athlete season statistics document request (season-scoped, not competition-scoped)
                        await PublishChildDocumentRequest(
                            command,
                            leaderDto.Statistics,
                            athleteSeasonIdentity.CanonicalId,
                            DocumentType.AthleteSeasonStatistics);
                    }

                    var stat = FranchiseSeasonLeaderStatExtensions.AsEntity(
                        leaderDto,
                        parentLeaderId: leaderEntity.Id,
                        athleteSeasonId: athleteSeasonId.Value,
                        correlationId: command.CorrelationId);

                    leaderEntity.Stats.Add(stat);
                }

                leaders.Add(leaderEntity);
            }
        }

        await _dataContext.FranchiseSeasonLeaders.AddRangeAsync(leaders);
        await _dataContext.SaveChangesAsync();

        var totalStats = leaders.Sum(l => l.Stats.Count);
        _logger.LogInformation("Persisted FranchiseSeasonLeaders. FranchiseSeasonId={FranchiseSeasonId}, Categories={CategoryCount}, Leaders={LeaderCount}, Stats={StatCount}",
            franchiseSeasonId,
            dto.Categories?.Count ?? 0,
            leaders.Count,
            totalStats);
    }

    /// <summary>
    /// Resolves an athlete season ID from an athlete season ref.
    /// Returns null if the athlete season is not found (allowing caller to batch missing dependencies).
    /// </summary>
    private async Task<Guid?> ResolveAthleteSeasonIdAsync(
        IHasRef athleteDto,
        ProcessDocumentCommand command,
        Dictionary<string, Guid> cache)
    {
        var key = athleteDto.Ref.ToString();

        if (cache.TryGetValue(key, out var cachedId))
            return cachedId;

        var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(athleteDto.Ref);

        var athleteSeason = await _dataContext.AthleteSeasons
            .Include(x => x.ExternalIds)
            .Where(s => s.ExternalIds.Any(e =>
                e.Provider == command.SourceDataProvider &&
                e.Value == athleteSeasonIdentity.UrlHash))
            .FirstOrDefaultAsync();

        if (athleteSeason is null)
        {
            // Return null to allow caller to collect all missing dependencies
            return null;
        }

        cache[key] = athleteSeason.Id;
        return athleteSeason.Id;
    }
}