using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeasonInjuries)]
public class TeamSeasonInjuriesDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public TeamSeasonInjuriesDocumentProcessor(
        ILogger<TeamSeasonInjuriesDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnTeamSeasonInjuryDto>();
        if (dto?.Id == null || dto.Ref == null)
        {
            _logger.LogWarning("Unable to deserialize document as EspnTeamSeasonInjuryDto");
            return;
        }

        if (dto.Athlete?.Ref is null)
        {
            _logger.LogWarning("Injury {InjuryId} has no athlete reference", dto.Id);
            return;
        }

        var headline = dto.GetHeadlineText();
        var text = dto.GetBodyText();

        if (string.IsNullOrEmpty(headline) || string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Injury {InjuryId} missing headline or text", dto.Id);
            return;
        }

        // Generate canonical ID from ESPN ref
        var injuryIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);
        
        // Find AthleteSeason by the athlete ref in the DTO
        var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Athlete.Ref);
        
        var athleteSeason = await _dataContext.AthleteSeasons
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == athleteSeasonIdentity.CanonicalId);

        if (athleteSeason is null)
        {
            _logger.LogWarning("AthleteSeason not found for athlete ref {AthleteRef}", dto.Athlete.Ref);
            return;
        }

        // Check if injury already exists
        var existing = await _dataContext.AthleteSeasonInjuries
            .FirstOrDefaultAsync(x => x.Id == injuryIdentity.CanonicalId);

        if (existing is null)
        {
            _logger.LogInformation("Processing new AthleteSeasonInjury entity. Ref={Ref}", dto.Ref);
            await ProcessNewEntity(command, dto, injuryIdentity, athleteSeason.Id);
        }
        else
        {
            _logger.LogInformation("Processing AthleteSeasonInjury update. InjuryId={InjuryId}, Ref={Ref}", existing.Id, dto.Ref);
            await ProcessUpdate(command, dto, existing);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnTeamSeasonInjuryDto dto,
        ExternalRefIdentity injuryIdentity,
        Guid athleteSeasonId)
    {
        _logger.LogInformation("Creating new AthleteSeasonInjury. Id={InjuryId}", injuryIdentity.CanonicalId);

        var injury = dto.AsEntity(injuryIdentity, athleteSeasonId, command.CorrelationId);
        
        await _dataContext.AthleteSeasonInjuries.AddAsync(injury);
        await _dataContext.SaveChangesAsync();
        
        _logger.LogInformation("AthleteSeasonInjury created. InjuryId={InjuryId}, AthleteSeasonId={AthleteSeasonId}", 
            injury.Id, athleteSeasonId);
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnTeamSeasonInjuryDto dto,
        AthleteSeasonInjury existing)
    {
        var hasChanges = false;

        var typeId = dto.Type?.Id ?? string.Empty;
        if (existing.TypeId != typeId)
        {
            existing.TypeId = typeId;
            hasChanges = true;
        }

        var typeName = dto.GetTypeName();
        if (existing.Type != typeName)
        {
            existing.Type = typeName;
            hasChanges = true;
        }

        var typeDescription = dto.Type?.Description;
        if (existing.TypeDescription != typeDescription)
        {
            existing.TypeDescription = typeDescription;
            hasChanges = true;
        }

        var typeAbbreviation = dto.Type?.Abbreviation;
        if (existing.TypeAbbreviation != typeAbbreviation)
        {
            existing.TypeAbbreviation = typeAbbreviation;
            hasChanges = true;
        }

        var headline = dto.GetHeadlineText();
        if (existing.Headline != headline)
        {
            existing.Headline = headline;
            hasChanges = true;
        }

        var text = dto.GetBodyText();
        if (existing.Text != text)
        {
            existing.Text = text;
            hasChanges = true;
        }

        if (existing.Date != dto.Date)
        {
            existing.Date = dto.Date;
            hasChanges = true;
        }

        var sourceName = dto.GetSourceName();
        if (existing.Source != sourceName)
        {
            existing.Source = sourceName;
            hasChanges = true;
        }

        if (existing.Status != dto.Status)
        {
            existing.Status = dto.Status;
            hasChanges = true;
        }

        if (hasChanges)
        {
            existing.ModifiedUtc = DateTime.UtcNow;
            existing.ModifiedBy = command.CorrelationId;
            
            await _dataContext.SaveChangesAsync();
            
            _logger.LogInformation("AthleteSeasonInjury updated. InjuryId={InjuryId}", existing.Id);
        }
        else
        {
            _logger.LogInformation("No property changes detected. InjuryId={InjuryId}", existing.Id);
        }
    }
}
