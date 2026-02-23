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

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
/// http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401628334/competitions/401628334/leaders?lang=en
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionLeaders)]
public class EventCompetitionLeadersDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{

    public EventCompetitionLeadersDocumentProcessor(
        ILogger<EventCompetitionLeadersDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalIdentityGenerator, refs) { }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnLeadersDto>();
        if (dto is null || string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("Invalid or null EspnLeadersDto.");
            return;
        }

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionLeadersRefToCompetitionRef);

        if (competitionId == null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        var competitionIdValue = competitionId.Value;

        var competition = await _dataContext.Competitions
            .Include(x => x.ExternalIds)
            .Include(x => x.Leaders)
            .ThenInclude(x => x.Stats)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == competitionIdValue);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionIdValue);
            return;
        }

        // delete existing leaders & stats
        var existingStatsCount = competition.Leaders.Sum(l => l.Stats.Count);
        var existingLeadersCount = competition.Leaders.Count;

        _dataContext.CompetitionLeaderStats.RemoveRange(
            competition.Leaders.SelectMany(l => l.Stats));
        _dataContext.CompetitionLeaders.RemoveRange(competition.Leaders);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Removed existing leaders. CompetitionId={CompId}, Leaders={LeaderCount}, Stats={StatCount}", 
            competitionId,
            existingLeadersCount,
            existingStatsCount);

        var franchiseSeasonCache = new Dictionary<string, Guid>();
        var athleteSeasonCache = new Dictionary<string, Guid>();

        var leaders = new List<CompetitionLeader>();

        foreach (var category in dto.Categories)
        {
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
                await _dataContext.SaveChangesAsync();
            }

            var leaderEntity = CompetitionLeaderExtensions.AsEntity(
                category,
                competitionIdValue,
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
                var franchiseSeasonId = await ResolveFranchiseSeasonIdAsync(leaderDto.Team, command, franchiseSeasonCache);

                var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(leaderDto.Athlete.Ref);

                // Use base class helper for consistency
                await PublishChildDocumentRequest(
                    command,
                    leaderDto.Statistics,
                    athleteSeasonIdentity.CanonicalId,
                    DocumentType.EventCompetitionAthleteStatistics);

                var stat = CompetitionLeaderStatExtensions.AsEntity(
                    leaderDto,
                    parentLeaderId: leaderEntity.Id,
                    athleteSeasonId: athleteSeasonId,
                    franchiseSeasonId: franchiseSeasonId,
                    correlationId: command.CorrelationId);

                leaderEntity.Stats.Add(stat);
            }

            leaders.Add(leaderEntity);
        }

        await _dataContext.CompetitionLeaders.AddRangeAsync(leaders);
        await _dataContext.SaveChangesAsync();

        var totalStats = leaders.Sum(l => l.Stats.Count);
        _logger.LogInformation("Persisted CompetitionLeaders. CompetitionId={CompId}, Categories={CategoryCount}, Leaders={LeaderCount}, Stats={StatCount}", 
            competitionId,
            dto.Categories.Count(),
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
            var athleteRef = EspnUriMapper.AthleteSeasonToAthleteRef(athleteDto.Ref);
            var athleteIdentity = _externalRefIdentityGenerator.Generate(athleteRef);

            await PublishChildDocumentRequest(
                command,
                athleteDto,
                athleteIdentity.CanonicalId.ToString(),
                DocumentType.AthleteSeason);

            throw new ExternalDocumentNotSourcedException(
                $"Missing AthleteSeason for ref {athleteDto.Ref}");
        }

        cache[key] = athleteSeason.Id;
        return athleteSeason.Id;
    }

    private async Task<Guid> ResolveFranchiseSeasonIdAsync(
        IHasRef teamDto,
        ProcessDocumentCommand command,
        Dictionary<string, Guid> cache)
    {
        var key = teamDto.Ref.ToString();

        if (cache.TryGetValue(key, out var cachedId))
            return cachedId;

        var franchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            teamDto,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id,
            CancellationToken.None);

        if (franchiseSeasonId is null)
        {
            var franchiseRef = EspnUriMapper.TeamSeasonToFranchiseRef(teamDto.Ref);
            var franchiseIdentity = _externalRefIdentityGenerator.Generate(franchiseRef);

            var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(teamDto.Ref);
                
            _logger.LogWarning("FranchiseSeason not found, requesting source. Hash={Hash}", franchiseSeasonIdentity.UrlHash);

            await PublishChildDocumentRequest(
                command,
                teamDto,
                franchiseIdentity.CanonicalId.ToString(),
                DocumentType.TeamSeason);

            throw new ExternalDocumentNotSourcedException($"Missing FranchiseSeason for ref {teamDto.Ref}");
        }

        cache[key] = franchiseSeasonId.Value;
        return franchiseSeasonId.Value;
    }
}