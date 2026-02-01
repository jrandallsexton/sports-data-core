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
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
///  http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752687/competitions/401752687/competitors/99/roster/4567747/statistics/0?lang=en&region=us
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionAthleteStatistics)]
public class EventCompetitionAthleteStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionAthleteStatisticsDocumentProcessor(
        ILogger<EventCompetitionAthleteStatisticsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Processing EventCompetitionAthleteStatistics with {@Command}", command);
            try
            {
                await ProcessInternal(command);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not yet sourced. Requeueing for retry. {@Command}", command);
                await _publishEndpoint.Publish(command.ToDocumentCreated(command.AttemptCount + 1));
                await _dataContext.SaveChangesAsync();
                return;
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
            _logger.LogWarning(
                "AthleteSeason not found for {AthleteSeasonRef}. Will retry when available.",
                dto.Athlete.Ref);
            throw new ExternalDocumentNotSourcedException(
                $"AthleteSeason {athleteSeasonIdentity.CleanUrl} not found. Will retry when available.");
        }

        // Resolve Competition
        var competitionIdentity = _externalRefIdentityGenerator.Generate(dto.Competition.Ref);

        var competition = await _dataContext.Competitions
            .Where(x => x.Id == competitionIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (competition is null)
        {
            _logger.LogWarning(
                "Competition not found for {CompetitionRef}. Will retry when available.",
                dto.Competition.Ref);
            throw new ExternalDocumentNotSourcedException(
                $"Competition {competitionIdentity.CleanUrl} not found. Will retry when available.");
        }

        // --- Generate Identity ---
        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

        // --- Remove Existing Statistics (ESPN replaces wholesale) ---
        var existing = await _dataContext.AthleteCompetitionStatistics
            .Include(x => x.Categories)
                .ThenInclude(c => c.Stats)
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

        if (existing is not null)
        {
            _logger.LogInformation("Removing existing AthleteCompetitionStatistic {Id} for replacement", existing.Id);
            _dataContext.AthleteCompetitionStatistics.Remove(existing);
            
            try
            {
                await _dataContext.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                _logger.LogWarning(
                    "Concurrency conflict removing AthleteCompetitionStatistic. Reloading and retrying. Id={Id}, CorrelationId={CorrelationId}",
                    existing.Id,
                    command.CorrelationId);

                // Detach all tracked Categories and their Stats (cascade deletes from parent removal)
                var trackedCategories = _dataContext.ChangeTracker.Entries<AthleteCompetitionStatisticCategory>()
                    .Where(e => e.Entity.AthleteCompetitionStatisticId == existing.Id)
                    .ToList();

                foreach (var category in trackedCategories)
                {
                    // Detach child Stats first
                    foreach (var stat in category.Entity.Stats)
                    {
                        var statEntry = _dataContext.Entry(stat);
                        if (statEntry.State != EntityState.Detached)
                        {
                            statEntry.State = EntityState.Detached;
                        }
                    }
                    
                    // Then detach the category
                    category.State = EntityState.Detached;
                }

                // Detach the parent entity
                _dataContext.Entry(existing).State = EntityState.Detached;

                // Reload fresh data
                existing = await _dataContext.AthleteCompetitionStatistics
                    .Include(x => x.Categories)
                        .ThenInclude(c => c.Stats)
                    .AsSplitQuery()
                    .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

                if (existing != null)
                {
                    // Retry the remove operation
                    _dataContext.AthleteCompetitionStatistics.Remove(existing);
                    await _dataContext.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning(
                        "AthleteCompetitionStatistic was deleted between concurrency exception and reload. Id={Id}, CorrelationId={CorrelationId}",
                        identity.CanonicalId,
                        command.CorrelationId);
                    return;
                }
            }
        }

        // --- Create New Entity ---
        var entity = dto.AsEntity(
            athleteSeason.Id,
            competition.Id,
            _externalRefIdentityGenerator,
            command.CorrelationId);

        await _dataContext.AthleteCompetitionStatistics.AddAsync(entity);

        // Save both remove and add in a single transaction
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Successfully processed AthleteCompetitionStatistic {Id} for AthleteSeason {AthleteSeasonId}, Competition {CompetitionId} with {CategoryCount} categories and {StatCount} total stats",
            entity.Id,
            athleteSeason.Id,
            competition.Id,
            entity.Categories.Count,
            entity.Categories.Sum(c => c.Stats.Count));
    }
}