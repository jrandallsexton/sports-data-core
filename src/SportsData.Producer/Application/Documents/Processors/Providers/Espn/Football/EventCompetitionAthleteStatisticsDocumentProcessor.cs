using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
///  http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752687/competitions/401752687/competitors/99/roster/4567747/statistics/0?lang=en&region=us
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionAthleteStatistics)]
public class EventCompetitionAthleteStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly DocumentProcessingConfig _config;

    public EventCompetitionAthleteStatisticsDocumentProcessor(
        ILogger<EventCompetitionAthleteStatisticsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator)
    {
        _config = config;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        // --- Deserialize DTO ---
        var dto = command.Document.FromJson<EspnEventCompetitionAthleteStatisticsDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EventCompetitionAthleteStatisticsDto. {@Command}", command);
            return;
        }

        if (dto.Ref is null)
        {
            _logger.LogError("EventCompetitionAthleteStatisticsDto Ref is null. {@Command}", command);
            return;
        }

        if (dto.Athlete?.Ref is null)
        {
            _logger.LogError("EventCompetitionAthleteStatisticsDto.Athlete.Ref is null. {@Command}", command);
            return;
        }

        if (dto.Competition?.Ref is null)
        {
            _logger.LogError("EventCompetitionAthleteStatisticsDto.Competition.Ref is null. {@Command}", command);
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command missing SeasonYear. {@Command}", command);
            return;
        }

        // --- Resolve Dependencies ---
        
        // Resolve AthleteSeason directly from dto.Athlete.Ref
        var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Athlete.Ref);
        
        var athleteSeason = await _dataContext.AthleteSeasons
            .Where(x => x.Id == athleteSeasonIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (athleteSeason is null)
        {
            if (!_config.EnableDependencyRequests)
            {
                throw new ExternalDocumentNotSourcedException(
                    $"AthleteSeason {athleteSeasonIdentity.CleanUrl} not found. Will retry when available.");
            }
            else
            {
                await PublishDependencyRequest<string?>(
                    command,
                    new EspnLinkDto { Ref = new Uri(athleteSeasonIdentity.CleanUrl) },
                    parentId: null,
                    DocumentType.AthleteSeason);

                throw new ExternalDocumentNotSourcedException(
                    $"AthleteSeason {athleteSeasonIdentity.CleanUrl} not found. Requested. Will retry.");
            }
        }

        // Resolve Competition
        var competitionIdentity = _externalRefIdentityGenerator.Generate(dto.Competition.Ref);

        var competition = await _dataContext.Competitions
            .Where(x => x.Id == competitionIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (competition is null)
        {
            if (!_config.EnableDependencyRequests)
            {
                throw new ExternalDocumentNotSourcedException(
                    $"Competition {competitionIdentity.CleanUrl} not found. Will retry when available.");
            }
            else
            {
                var contestRef = EspnUriMapper.CompetitionRefToContestRef(dto.Competition.Ref);
                var contestIdentity = _externalRefIdentityGenerator.Generate(contestRef);

                await PublishDependencyRequest<Guid>(
                    command,
                    new EspnLinkDto { Ref = dto.Competition.Ref },
                    parentId: contestIdentity.CanonicalId,
                    DocumentType.EventCompetition);

                throw new ExternalDocumentNotSourcedException(
                    $"Competition {competitionIdentity.CleanUrl} not found. Requested. Will retry.");
            }
        }

        // --- Generate Identity ---
        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

        // --- Wholesale Replacement (Remove + Create in single transaction) ---
        var existing = await _dataContext.AthleteCompetitionStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

        if (existing is not null)
        {
            _logger.LogInformation("Removing existing AthleteCompetitionStatistic {Id} for replacement", existing.Id);
            _dataContext.AthleteCompetitionStatistics.Remove(existing);
        }

        // --- Create New Entity ---
        var entity = dto.AsEntity(
            athleteSeason.Id,
            competition.Id,
            _externalRefIdentityGenerator,
            command.CorrelationId);

        await _dataContext.AthleteCompetitionStatistics.AddAsync(entity);

        // Save BOTH remove and add in a SINGLE transaction
        try
        {
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException) when (existing != null)
        {
            // Concurrency conflict on the delete - another pod updated/deleted between our load and save
            _logger.LogWarning(
                "Concurrency conflict on wholesale replacement. Reloading and retrying. Id={Id}, CorrelationId={CorrelationId}",
                existing.Id,
                command.CorrelationId);

            // Detach all tracked entities
            var trackedCategories = _dataContext.ChangeTracker.Entries<AthleteCompetitionStatisticCategory>()
                .Where(e => e.Entity.AthleteCompetitionStatisticId == existing.Id)
                .ToList();

            foreach (var category in trackedCategories)
            {
                // Materialize collection before iteration to prevent "Collection was modified" exception
                foreach (var stat in category.Entity.Stats.ToList())
                {
                    var statEntry = _dataContext.Entry(stat);
                    if (statEntry.State != EntityState.Detached)
                    {
                        statEntry.State = EntityState.Detached;
                    }
                }
                category.State = EntityState.Detached;
            }

            _dataContext.Entry(existing).State = EntityState.Detached;
            _dataContext.Entry(entity).State = EntityState.Detached;

            // Reload and retry
            existing = await _dataContext.AthleteCompetitionStatistics
                .Include(x => x.Categories)
                    .ThenInclude(c => c.Stats)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

            if (existing != null)
            {
                _dataContext.AthleteCompetitionStatistics.Remove(existing);
            }

            // Recreate entity (entity object was detached)
            entity = dto.AsEntity(
                athleteSeason.Id,
                competition.Id,
                _externalRefIdentityGenerator,
                command.CorrelationId);

            await _dataContext.AthleteCompetitionStatistics.AddAsync(entity);
            await _dataContext.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (
            ex.InnerException?.Message?.Contains("duplicate key") == true ||
            ex.InnerException?.Message?.Contains("PK_AthleteCompetitionStatistic") == true)
        {
            // Another pod already created this entity - they won the race
            _logger.LogWarning(
                "Duplicate key on wholesale replacement. Another process already created it. " +
                "Id={Id}, CorrelationId={CorrelationId}",
                entity.Id,
                command.CorrelationId);
            return;
        }

        _logger.LogInformation(
            "Successfully processed AthleteCompetitionStatistic {Id} for AthleteSeason {AthleteSeasonId}, Competition {CompetitionId} with {CategoryCount} categories and {StatCount} total stats",
            entity.Id,
            athleteSeason.Id,
            competition.Id,
            entity.Categories.Count,
            entity.Categories.Sum(c => c.Stats.Count));
    }
}