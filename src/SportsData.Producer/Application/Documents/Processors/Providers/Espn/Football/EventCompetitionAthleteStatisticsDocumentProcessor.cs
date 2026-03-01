using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
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
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator) { }

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
            await PublishDependencyRequest<string?>(
                command,
                new EspnLinkDto { Ref = new Uri(athleteSeasonIdentity.CleanUrl) },
                parentId: null,
                DocumentType.AthleteSeason);

            throw new ExternalDocumentNotSourcedException(
                $"AthleteSeason {athleteSeasonIdentity.CleanUrl} not found. Requested. Will retry.");
        }

        // Resolve Competition
        var competitionIdentity = _externalRefIdentityGenerator.Generate(dto.Competition.Ref);

        var competition = await _dataContext.Competitions
            .Where(x => x.Id == competitionIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (competition is null)
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

        // --- Generate Identity ---
        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

        // --- Wholesale Replacement (Remove + Create in single transaction) ---
        // Uses a retry loop because the xmin-based optimistic concurrency check on the DELETE
        // can fail if another pod modifies the row between our load and our save. A single
        // nested catch is not sufficient — the retry itself can also encounter contention.
        // TODO: Consider externalizing maxConcurrencyRetries to Azure AppConfig so it can be
        // tuned without redeployment (key: SportsData.Producer:Processing:MaxConcurrencyRetries).
        const int maxConcurrencyRetries = 3;
        AthleteCompetitionStatistic? entity = null;

        for (var attempt = 0; attempt < maxConcurrencyRetries; attempt++)
        {
            // Always reload from DB on every attempt so xmin is fresh
            var existing = await _dataContext.AthleteCompetitionStatistics
                .Include(x => x.Categories)
                    .ThenInclude(c => c.Stats)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

            if (existing is not null)
            {
                _logger.LogInformation(
                    "Removing existing AthleteCompetitionStatistic {Id} for replacement (attempt {Attempt})",
                    existing.Id, attempt + 1);
                _dataContext.AthleteCompetitionStatistics.Remove(existing);
            }

            entity = dto.AsEntity(
                athleteSeason.Id,
                competition.Id,
                _externalRefIdentityGenerator,
                command.CorrelationId);

            await _dataContext.AthleteCompetitionStatistics.AddAsync(entity);

            try
            {
                await _dataContext.SaveChangesAsync();
                break; // success — exit the retry loop
            }
            catch (DbUpdateConcurrencyException) when (existing != null && attempt < maxConcurrencyRetries - 1)
            {
                // DELETE hit stale xmin — another pod modified the row between our load and save.
                // Detach everything and loop to reload with a fresh xmin.
                _logger.LogWarning(
                    "Concurrency conflict on wholesale replacement (attempt {Attempt}/{Max}). " +
                    "Reloading with fresh xmin. Id={Id}, CorrelationId={CorrelationId}",
                    attempt + 1, maxConcurrencyRetries, existing.Id, command.CorrelationId);

                var trackedCategories = _dataContext.ChangeTracker.Entries<AthleteCompetitionStatisticCategory>()
                    .Where(e => e.Entity.AthleteCompetitionStatisticId == existing.Id)
                    .ToList();

                foreach (var category in trackedCategories)
                {
                    foreach (var stat in category.Entity.Stats.ToList())
                    {
                        var statEntry = _dataContext.Entry(stat);
                        if (statEntry.State != EntityState.Detached)
                            statEntry.State = EntityState.Detached;
                    }
                    category.State = EntityState.Detached;
                }

                _dataContext.Entry(existing).State = EntityState.Detached;
                _dataContext.Entry(entity).State = EntityState.Detached;
                // continue loop — next iteration reloads with a fresh xmin
            }
            catch (DbUpdateException ex) when (ex.IsUniqueConstraintViolation())
            {
                // Another pod won the race and already inserted this entity.
                _logger.LogWarning(
                    "Duplicate key on wholesale replacement — another process already created it. " +
                    "Id={Id}, CorrelationId={CorrelationId}",
                    entity.Id, command.CorrelationId);

                // Detach all dirty entries — EF still tracks entity (Added) and existing (Deleted)
                // after the failed SaveChangesAsync. Leave the DbContext clean in case the scope
                // is reused.
                if (existing is not null)
                {
                    var trackedCategories = _dataContext.ChangeTracker.Entries<AthleteCompetitionStatisticCategory>()
                        .Where(e => e.Entity.AthleteCompetitionStatisticId == existing.Id)
                        .ToList();
                    foreach (var category in trackedCategories)
                    {
                        foreach (var stat in category.Entity.Stats.ToList())
                            _dataContext.Entry(stat).State = EntityState.Detached;
                        category.State = EntityState.Detached;
                    }
                    _dataContext.Entry(existing).State = EntityState.Detached;
                }

                foreach (var category in entity.Categories)
                {
                    foreach (var stat in category.Stats)
                        _dataContext.Entry(stat).State = EntityState.Detached;
                    _dataContext.Entry(category).State = EntityState.Detached;
                }
                _dataContext.Entry(entity).State = EntityState.Detached;

                return;
            }
        }

        _logger.LogInformation(
            "Successfully processed AthleteCompetitionStatistic {Id} for AthleteSeason {AthleteSeasonId}, Competition {CompetitionId} with {CategoryCount} categories and {StatCount} total stats",
            entity!.Id,
            athleteSeason.Id,
            competition.Id,
            entity.Categories.Count,
            entity.Categories.Sum(c => c.Stats.Count));
    }
}