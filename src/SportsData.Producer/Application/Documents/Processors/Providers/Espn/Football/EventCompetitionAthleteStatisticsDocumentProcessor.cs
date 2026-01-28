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
        
        // Resolve Athlete (to get AthleteId)
        var athleteRef = EspnUriMapper.AthleteSeasonToAthleteRef(dto.Athlete.Ref);
        var athleteIdentity = _externalRefIdentityGenerator.Generate(athleteRef);

        var athlete = await _dataContext.Athletes
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == athleteIdentity.CanonicalId);

        if (athlete is null)
        {
            _logger.LogError("Athlete not found: {AthleteId}. Ref={AthleteRef}", 
                athleteIdentity.CanonicalId, 
                athleteRef);
            return;
        }

        // Resolve AthleteSeason (athlete + season)
        // Note: AthleteSeason links to FranchiseSeason via FranchiseSeasonId (Guid only, no navigation property)
        var athleteSeason = await (from ats in _dataContext.AthleteSeasons
                                   join fs in _dataContext.FranchiseSeasons on ats.FranchiseSeasonId equals fs.Id
                                   where ats.AthleteId == athleteIdentity.CanonicalId 
                                      && fs.SeasonYear == command.Season.Value
                                   select ats)
            .AsNoTracking()
            .FirstOrDefaultAsync();

        if (athleteSeason is null)
        {
            _logger.LogError("AthleteSeason not found for Athlete={AthleteId}, Season={Season}", 
                athleteIdentity.CanonicalId, 
                command.Season.Value);
            return;
        }

        // Resolve Competition
        var competitionIdentity = _externalRefIdentityGenerator.Generate(dto.Competition.Ref);

        var competition = await _dataContext.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == competitionIdentity.CanonicalId);

        if (competition is null)
        {
            _logger.LogError("Competition not found: {CompetitionId}. Ref={CompetitionRef}", 
                competitionIdentity.CanonicalId, 
                dto.Competition.Ref);
            return;
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