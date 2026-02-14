using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetition)]
public class EventCompetitionDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{

    public EventCompetitionDocumentProcessor(
        ILogger<EventCompetitionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["DocumentType"] = command.DocumentType,
                   ["Season"] = command.Season ?? 0,
                   ["CompetitionId"] = command.ParentId ?? "Unknown"
               }))
        {
            _logger.LogInformation("EventCompetitionDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}", 
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionDocumentProcessor completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<EspnEventCompetitionDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionDto.");
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionDto Ref is null.");
            return;
        }

        if (string.IsNullOrEmpty(command.ParentId))
        {
            _logger.LogError("ParentId not provided. Cannot process competition for null ContestId.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var contestId))
        {
            _logger.LogError("Invalid ParentId format for ContestId. Cannot parse to Guid.");
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            return;
        }

        var contest = await _dataContext.Contests
            .FirstOrDefaultAsync(c => c.Id == contestId);

        if (contest is null)
        {
            _logger.LogError("Contest not found. ContestId={ContestId}", contestId);
            throw new InvalidOperationException($"Contest with ID {contestId} not found.");
        }

        var entity = await _dataContext.Competitions
            .Include(c => c.Competitors)
            .ThenInclude(c => c.ExternalIds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x =>
                x.ExternalIds.Any(z => z.SourceUrlHash == command.UrlHash &&
                                       z.Provider == command.SourceDataProvider));

        if (entity is null)
        {
            _logger.LogInformation("Processing new Competition entity. Ref={Ref}", externalDto.Ref);
            await ProcessNewEntity(command, externalDto, command.Season.Value, contestId);
        }
        else
        {
            _logger.LogInformation("Processing Competition update. CompetitionId={CompetitionId}, Ref={Ref}", entity.Id, externalDto.Ref);
            await ProcessUpdate(command, externalDto, entity);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        int seasonYear,
        Guid contestId)
    {
        _logger.LogInformation("Creating new Competition. ContestId={ContestId}", contestId);

        var competition = externalDto.AsEntity(
            _externalRefIdentityGenerator,
            contestId,
            command.CorrelationId);

        await AddVenue(command, externalDto, competition);

        ProcessNotes(command, externalDto, competition);
        ProcessLinks(command, externalDto, competition);

        await _dataContext.Competitions.AddAsync(competition);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Competition created. CompetitionId={CompetitionId}", competition.Id);
        
        // Process all child documents - always spawn all children for new entities
        await ProcessChildDocuments(command, externalDto, competition, isNew: true);
    }

    private async Task AddVenue(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        Competition competition)
    {
        var venue = externalDto.Venue;

        if (venue?.Ref is null)
        {
            _logger.LogDebug("No venue information provided in the competition document.");
            return;
        }

        var venueId = await _dataContext.ResolveIdAsync<
            Venue, VenueExternalId>(
            venue,
            command.SourceDataProvider,
            () => _dataContext.Venues,
            externalIdsNav: "ExternalIds",
            key: v => v.Id);

        if (venueId != null)
        {
            competition.VenueId = venueId.Value;
        }
        else
        {
            var venueHash = HashProvider.GenerateHashFromUri(venue.Ref);
            _logger.LogWarning("Venue not found, publishing sourcing request. VenueHash={VenueHash}", venueHash);
            
            await PublishChildDocumentRequest<string?>(
                command,
                venue,
                parentId: null,
                DocumentType.Venue);
        }
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto dto,
        Competition competition)
    {
        // Map DTO to a temporary entity with all properties populated
        var updatedEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competition.ContestId,
            command.CorrelationId);

        // Store the original date for comparison (needed for ContestStartTimeUpdated event)
        var originalDate = competition.Date;

        // Preserve immutable fields before SetValues overwrites them
        var originalCreatedBy = competition.CreatedBy;
        var originalCreatedUtc = competition.CreatedUtc;

        // Use EF Core's SetValues to update all scalar properties automatically
        // This compares every property and marks only changed ones as Modified
        _dataContext.Entry(competition).CurrentValues.SetValues(updatedEntity);

        // Restore immutable fields - SetValues overwrites in-memory values
        competition.CreatedBy = originalCreatedBy;
        competition.CreatedUtc = originalCreatedUtc;

        // Check if EF detected any changes
        if (_dataContext.Entry(competition).State == EntityState.Modified)
        {
            // Log which properties changed (helpful for debugging)
            var changedProperties = _dataContext.Entry(competition)
                .Properties
                .Where(p => p.IsModified)
                .Select(p => $"{p.Metadata.Name}: {p.OriginalValue} -> {p.CurrentValue}")
                .ToList();

            _logger.LogInformation(
                "Updating Competition. CompetitionId={CompetitionId}, ChangedProperties={Changes}",
                competition.Id,
                string.Join(", ", changedProperties));

            // Special handling: Publish domain event if Date changed
            if (competition.Date != originalDate)
            {
                _logger.LogInformation(
                    "Competition date changed. OldDate={OldDate}, NewDate={NewDate}",
                    originalDate, 
                    competition.Date);

                await _publishEndpoint.Publish(
                    new ContestStartTimeUpdated(
                        competition.ContestId,
                        competition.Date,
                        null,
                        command.Sport,
                        command.Season,
                        command.CorrelationId,
                        CausationId.Producer.EventCompetitionDocumentProcessor));
            }

            // Update audit fields
            competition.ModifiedUtc = DateTime.UtcNow;
            competition.ModifiedBy = command.CorrelationId;

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Competition updated. CompetitionId={CompetitionId}, PropertyCount={PropertyCount}",
                competition.Id,
                changedProperties.Count);
        }
        else
        {
            _logger.LogInformation("No property changes detected. CompetitionId={CompetitionId}", competition.Id);
        }

        // Process child documents - respect ShouldSpawn filtering for updates
        await ProcessChildDocuments(command, dto, competition, isNew: false);
    }

    /// <summary>
    /// Processes all child documents and relationships for a competition.
    /// For new entities (isNew=true), always spawns all child documents.
    /// For updates (isNew=false), respects ShouldSpawn filtering.
    /// </summary>
    private async Task ProcessChildDocuments(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto dto,
        Competition competition,
        bool isNew)
    {
        _logger.LogInformation("Processing child documents for Competition. CompetitionId={CompId}, IsNew={IsNew}", competition.Id, isNew);

        // All child documents - bypass ShouldSpawn for new entities, apply filtering for updates
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionOdds, command))
            await PublishChildDocumentRequest(command, dto.Odds, competition.Id, DocumentType.EventCompetitionOdds);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionStatus, command))
            await PublishChildDocumentRequest(command, dto.Status, competition.Id, DocumentType.EventCompetitionStatus);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionSituation, command))
            await PublishChildDocumentRequest(command, dto.Situation, competition.Id, DocumentType.EventCompetitionSituation);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionBroadcast, command))
            await PublishChildDocumentRequest(command, dto.Broadcasts, competition.Id, DocumentType.EventCompetitionBroadcast);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionPlay, command))
            await PublishChildDocumentRequest(command, dto.Details, competition.Id, DocumentType.EventCompetitionPlay);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionLeaders, command))
            await PublishChildDocumentRequest(command, dto.Leaders, competition.Id, DocumentType.EventCompetitionLeaders);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionPrediction, command))
            await PublishChildDocumentRequest(command, dto.Predictor, competition.Id, DocumentType.EventCompetitionPrediction);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionProbability, command))
            await PublishChildDocumentRequest(command, dto.Probabilities, competition.Id, DocumentType.EventCompetitionProbability);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionPowerIndex, command))
            await PublishChildDocumentRequest(command, dto.PowerIndexes, competition.Id, DocumentType.EventCompetitionPowerIndex);
        if (isNew || ShouldSpawn(DocumentType.EventCompetitionDrive, command))
            await PublishChildDocumentRequest(command, dto.Drives, competition.Id, DocumentType.EventCompetitionDrive);

        if (isNew || ShouldSpawn(DocumentType.EventCompetitionCompetitor, command))
            await ProcessCompetitors(command, dto, competition);

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Completed processing child documents for Competition. CompetitionId={CompId}", competition.Id);
    }

    private async Task ProcessCompetitors(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        Competition competition)
    {
        _logger.LogInformation("Requesting {Count} competitors. CompetitionId={CompId}", 
            externalDto.Competitors.Count, 
            competition.Id);

        foreach (var competitorDto in externalDto.Competitors)
        {
            if (competitorDto?.Ref is null)
            {
                _logger.LogWarning("Competitor reference is null, skipping.");
                continue;
            }

            _logger.LogDebug("Requesting competitor. CompetitionId={CompId}, CompetitorRef={Ref}", 
                competition.Id,
                competitorDto.Ref);

            await PublishChildDocumentRequest(
                command,
                competitorDto,
                competition.Id.ToString(),
                DocumentType.EventCompetitionCompetitor);
        }
    }

    private static void ProcessNotes(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        Competition competition)
    {
        if (!externalDto.Notes.Any())
        {
            return;
        }

        foreach (var note in externalDto.Notes)
        {
            var newNote = new CompetitionNote
            {
                Type = note.Type,
                Headline = note.Headline,
                CompetitionId = competition.Id
            };
            competition.Notes.Add(newNote);
        }
    }

    private static void ProcessLinks(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        Competition competition)
    {
        if (externalDto.Links?.Any() != true)
            return;

        foreach (var link in externalDto.Links)
        {
            competition.Links.Add(new CompetitionLink()
            {
                Id = Guid.NewGuid(),
                CompetitionId = competition.Id,
                Rel = string.Join("|", link.Rel),
                Href = link.Href.ToCleanUrl(),
                Text = link.Text,
                ShortText = link.ShortText,
                IsExternal = link.IsExternal,
                IsPremium = link.IsPremium,
                SourceUrlHash = HashProvider.GenerateHashFromUri(link.Href)
            });
        }
    }
}