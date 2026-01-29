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
using SportsData.Producer.Config;
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
    private readonly DocumentProcessingConfig _config;

    public TeamSeasonLeadersDocumentProcessor(
        ILogger<TeamSeasonLeadersDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _config = config;
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId,
            ["DocumentType"] = command.DocumentType,
            ["Season"] = command.Season ?? 0,
            ["FranchiseSeasonId"] = command.ParentId ?? "Unknown"
        }))
        {
            _logger.LogInformation("TeamSeasonLeadersDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}",
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);

                _logger.LogInformation("TeamSeasonLeadersDocumentProcessor completed.");
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready, will retry later.");

                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TeamSeasonLeadersDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
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
            _logger.LogError("FranchiseSeason not found. FranchiseSeasonId={FranchiseSeasonId}", franchiseSeasonId);
            return;
        }

        // Delete existing leaders & stats (wholesale replacement pattern)
        var existingStatsCount = franchiseSeason.Leaders.Sum(l => l.Stats.Count);
        var existingLeadersCount = franchiseSeason.Leaders.Count;

        _dataContext.FranchiseSeasonLeaderStats.RemoveRange(
            franchiseSeason.Leaders.SelectMany(l => l.Stats));
        _dataContext.FranchiseSeasonLeaders.RemoveRange(franchiseSeason.Leaders);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Removed existing leaders. FranchiseSeasonId={FranchiseSeasonId}, Leaders={LeaderCount}, Stats={StatCount}",
            franchiseSeasonId,
            existingLeadersCount,
            existingStatsCount);

        var athleteSeasonCache = new Dictionary<string, Guid>();
        var leaders = new List<FranchiseSeasonLeader>();

        foreach (var category in dto.Categories)
        {
            var leaderCategory = await _dataContext.LeaderCategories
                .FirstOrDefaultAsync(x => x.Name == category.Name);

            if (leaderCategory == null)
            {
                _logger.LogInformation("Leader category not found, creating new category. Category={Category}, DisplayName={DisplayName}",
                    category.Name,
                    category.DisplayName);

                leaderCategory = new CompetitionLeaderCategory
                {
                    Name = category.Name,
                    DisplayName = category.DisplayName,
                    ShortDisplayName = category.ShortDisplayName ?? category.DisplayName,
                    Abbreviation = category.Abbreviation ?? category.DisplayName,
                    CreatedUtc = DateTime.UtcNow,
                    CreatedBy = command.CorrelationId
                };

                _dataContext.LeaderCategories.Add(leaderCategory);
                await _dataContext.SaveChangesAsync(); // Save to get the generated Id
            }

            var leaderEntity = FranchiseSeasonLeaderExtensions.AsEntity(
                category,
                franchiseSeasonId,
                leaderCategory.Id,
                command.CorrelationId
            );

            foreach (var leaderDto in category.Leaders)
            {
                if (leaderDto.Statistics?.Ref is null)
                {
                    _logger.LogDebug("Leader statistics ref is null, skipping.");
                    continue;
                }

                var athleteSeasonId = await ResolveAthleteSeasonIdAsync(leaderDto.Athlete, command, athleteSeasonCache);

                var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(leaderDto.Athlete.Ref);

                // Spawn athlete statistics document request
                await PublishChildDocumentRequest(
                    command,
                    leaderDto.Statistics,
                    athleteSeasonIdentity.CanonicalId,
                    DocumentType.EventCompetitionAthleteStatistics,
                    CausationId.Producer.TeamSeasonLeadersDocumentProcessor);

                var stat = FranchiseSeasonLeaderStatExtensions.AsEntity(
                    leaderDto,
                    parentLeaderId: leaderEntity.Id,
                    athleteSeasonId: athleteSeasonId,
                    franchiseSeasonId: franchiseSeasonId,
                    correlationId: command.CorrelationId);

                leaderEntity.Stats.Add(stat);
            }

            leaders.Add(leaderEntity);
        }

        await _dataContext.FranchiseSeasonLeaders.AddRangeAsync(leaders);
        await _dataContext.SaveChangesAsync();

        var totalStats = leaders.Sum(l => l.Stats.Count);
        _logger.LogInformation("Persisted FranchiseSeasonLeaders. FranchiseSeasonId={FranchiseSeasonId}, Categories={CategoryCount}, Leaders={LeaderCount}, Stats={StatCount}",
            franchiseSeasonId,
            dto.Categories.Count,
            leaders.Count,
            totalStats);
    }

    private async Task<Guid> ResolveAthleteSeasonIdAsync(
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
            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.AthleteSeason,
                    nameof(TeamSeasonLeadersDocumentProcessor<TDataContext>),
                    athleteSeasonIdentity.CleanUrl);
                throw new ExternalDocumentNotSourcedException(
                    $"AthleteSeason {athleteSeasonIdentity.CleanUrl} not found. Will retry when available.");
            }
            else
            {
                var athleteRef = EspnUriMapper.AthleteSeasonToAthleteRef(athleteDto.Ref);
                var athleteIdentity = _externalRefIdentityGenerator.Generate(athleteRef);

                _logger.LogWarning("AthleteSeason not found, raising DocumentRequested. Url={Url}", athleteSeasonIdentity.CleanUrl);

                await PublishChildDocumentRequest(
                    command,
                    athleteDto,
                    athleteIdentity.CanonicalId.ToString(),
                    DocumentType.AthleteSeason,
                    CausationId.Producer.TeamSeasonLeadersDocumentProcessor);

                throw new ExternalDocumentNotSourcedException(
                    $"Missing AthleteSeason for ref {athleteDto.Ref}");
            }
        }

        cache[key] = athleteSeason.Id;
        return athleteSeason.Id;
    }
}