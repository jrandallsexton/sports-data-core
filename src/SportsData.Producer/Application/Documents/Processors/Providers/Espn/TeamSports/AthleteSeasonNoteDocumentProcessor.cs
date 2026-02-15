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

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteSeasonNote)]
public class AthleteSeasonNoteDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public AthleteSeasonNoteDocumentProcessor(
        ILogger<AthleteSeasonNoteDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnAthleteSeasonNoteDto>();
        if (dto?.Id == null || dto.Ref == null)
        {
            _logger.LogWarning("Unable to deserialize document as EspnAthleteSeasonNoteDto");
            return;
        }

        if (dto.Athlete?.Ref is null)
        {
            _logger.LogWarning("Note {NoteId} has no athlete reference", dto.Id);
            return;
        }

        var headline = dto.GetHeadlineText();
        var text = dto.GetBodyText();

        if (string.IsNullOrEmpty(headline) || string.IsNullOrEmpty(text))
        {
            _logger.LogWarning("Note {NoteId} missing headline or text", dto.Id);
            return;
        }

        // Resolve AthleteSeason from athlete reference
        var athleteSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Athlete.Ref);
        var athleteSeason = await _dataContext.AthleteSeasons
            .AsNoTracking()
            .Where(x => x.Id == athleteSeasonIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (athleteSeason is null)
        {
            _logger.LogWarning(
                "AthleteSeason not found for note. AthleteSeasonId={AthleteSeasonId}, NoteId={NoteId}",
                athleteSeasonIdentity.CanonicalId,
                dto.Id);
            return;
        }

        var noteIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var existing = await _dataContext.AthleteSeasonNotes
            .Where(x => x.Id == noteIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            await ProcessNewEntity(command, dto, noteIdentity, athleteSeason.Id);
        }
        else
        {
            await ProcessUpdate(command, dto, existing);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnAthleteSeasonNoteDto dto,
        ExternalRefIdentity noteIdentity,
        Guid athleteSeasonId)
    {
        _logger.LogInformation("Creating new AthleteSeasonNote. Id={NoteId}", noteIdentity.CanonicalId);

        var note = dto.AsEntity(noteIdentity, athleteSeasonId, command.CorrelationId);
        
        await _dataContext.AthleteSeasonNotes.AddAsync(note);
        await _dataContext.SaveChangesAsync();
        
        _logger.LogInformation("AthleteSeasonNote created. NoteId={NoteId}, AthleteSeasonId={AthleteSeasonId}", 
            note.Id, athleteSeasonId);
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnAthleteSeasonNoteDto dto,
        AthleteSeasonNote existing)
    {
        var hasChanges = false;

        var typeName = dto.GetTypeName();
        if (existing.Type != typeName)
        {
            existing.Type = typeName;
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

        if (existing.Source != dto.Source)
        {
            existing.Source = dto.Source;
            hasChanges = true;
        }

        if (hasChanges)
        {
            existing.ModifiedUtc = DateTime.UtcNow;
            existing.ModifiedBy = command.CorrelationId;
            
            await _dataContext.SaveChangesAsync();
            
            _logger.LogInformation("AthleteSeasonNote updated. NoteId={NoteId}", existing.Id);
        }
        else
        {
            _logger.LogInformation("No property changes detected. NoteId={NoteId}", existing.Id);
        }
    }
}
