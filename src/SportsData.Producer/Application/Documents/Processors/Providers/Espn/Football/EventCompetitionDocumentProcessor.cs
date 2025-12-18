using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Clients.Provider;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetition)]
public class EventCompetitionDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : FootballDataContext
{
    private readonly ILogger<EventDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly IProvideProviders _provider;

    public EventCompetitionDocumentProcessor(
        ILogger<EventDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IProvideProviders provider
    )
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _provider = provider;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Began with {@command}", command);

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
        var externalDto = command.Document.FromJson<EspnEventCompetitionDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnEventCompetitionDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionDto Ref is null. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(command.ParentId))
        {
            _logger.LogError("ParentId not provided. Cannot process competition for null ContestId");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var contestId))
        {
            _logger.LogError("Invalid ParentId format for ContestId. Cannot parse to Guid.");
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command must have a SeasonYear defined");
            return;
        }

        var contest = await _dataContext.Contests
            .FirstOrDefaultAsync(c => c.Id == contestId);

        if (contest is null)
        {
            _logger.LogError("Contest not found.");
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
            await ProcessNewEntity(
                command,
                externalDto,
                command.Season.Value,
                contestId);
        }
        else
        {
            await ProcessUpdate(
                command,
                externalDto,
                entity);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        int seasonYear,
        Guid contestId)
    {
        var competition = externalDto.AsEntity(
            _externalRefIdentityGenerator,
            contestId,
            command.CorrelationId);

        await AddVenue(command, externalDto, competition);

        ProcessNotes(command, externalDto, competition);
        ProcessLinks(command, externalDto, competition);

        await _dataContext.Competitions.AddAsync(competition);
        await _dataContext.SaveChangesAsync();

        // Process all child documents - same logic whether new or update
        await ProcessChildDocuments(command, externalDto, competition);
    }

    private async Task AddVenue(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        Competition competition)
    {
        var venue = externalDto.Venue;

        if (venue?.Ref is null)
        {
            _logger.LogWarning("No venue information provided in the competition document.");
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
            _logger.LogWarning("Venue not found for hash {VenueHash}, publishing sourcing request.", venueHash);
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: venueHash,
                ParentId: null,
                Uri: venue.Ref.ToCleanUri(),
                Sport: Sport.FootballNcaa,
                SeasonYear: command.Season,
                DocumentType: DocumentType.Venue,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventDocumentProcessor
            ));
        }
    }

    private async Task ProcessCompetitors(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        Competition competition)
    {
        foreach (var competitorDto in externalDto.Competitors)
        {
            if (competitorDto?.Ref is null)
            {
                _logger.LogError("Competitor reference is null, skipping competitor processing.");
                continue;
            }

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: HashProvider.GenerateHashFromUri(competitorDto.Ref),
                ParentId: competition.Id.ToString(),
                Uri: competitorDto.Ref.ToCleanUri(),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionCompetitor,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));
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

    private async Task<bool> ProcessSituation(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        Competition competition)
    {
        if (externalDto.Situation?.Ref is not null)
        {
            var situationIdentity = _externalRefIdentityGenerator.Generate(externalDto.Situation.Ref);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: situationIdentity.UrlHash,
                ParentId: competition.Id.ToString(),
                Uri: new Uri(situationIdentity.CleanUrl),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetitionSituation,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
            ));

            return true;
        }

        return false;
    }

    private async Task<bool> ProcessStatus(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto externalDto,
        Competition competition)
    {
        if (externalDto.Status?.Ref is null)
        {
            _logger.LogWarning("No status information provided in the competition document.");
            return false;
        }
            
        await _publishEndpoint.Publish(new DocumentRequested(
            Id: HashProvider.GenerateHashFromUri(externalDto.Status.Ref),
            ParentId: competition.Id.ToString(),
            Uri: externalDto.Status.Ref,
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: DocumentType.EventCompetitionStatus,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
        ));

        return true;
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

    /// <summary>
    /// Generic helper to process child documents that have a $ref property.
    /// Eliminates repetitive null checks and DocumentRequested publishing.
    /// </summary>
    private async Task ProcessChildDocumentRef(
        ProcessDocumentCommand command,
        EspnLinkDto? linkDto,
        Competition competition,
        DocumentType documentType)
    {
        if (linkDto?.Ref is null)
            return;

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: HashProvider.GenerateHashFromUri(linkDto.Ref),
            ParentId: competition.Id.ToString(),
            Uri: linkDto.Ref.ToCleanUri(),
            Sport: command.Sport,
            SeasonYear: command.Season,
            DocumentType: documentType,
            SourceDataProvider: command.SourceDataProvider,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionDocumentProcessor
        ));
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

        // Use EF Core's SetValues to update all scalar properties automatically
        // This compares every property and marks only changed ones as Modified
        _dataContext.Entry(competition).CurrentValues.SetValues(updatedEntity);

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
                "Updating Competition {CompetitionId}. Changed properties: {Changes}",
                competition.Id,
                string.Join(", ", changedProperties));

            // Special handling: Publish domain event if Date changed
            if (competition.Date != originalDate)
            {
                _logger.LogInformation(
                    "Competition date changed from {OldDate} to {NewDate}, publishing ContestStartTimeUpdated",
                    originalDate, competition.Date);

                await _publishEndpoint.Publish(
                    new ContestStartTimeUpdated(
                        competition.ContestId,
                        competition.Date,
                        command.CorrelationId,
                        CausationId.Producer.EventCompetitionDocumentProcessor));
            }

            // Update audit fields
            competition.ModifiedUtc = DateTime.UtcNow;
            competition.ModifiedBy = command.CorrelationId;

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Updated Competition {CompetitionId} with {PropertyCount} property changes",
                competition.Id,
                changedProperties.Count);
        }
        else
        {
            _logger.LogInformation(
                "No property changes detected for Competition {CompetitionId}",
                competition.Id);
        }

        // Process all child documents - same logic whether new or update
        await ProcessChildDocuments(command, dto, competition);
    }

    /// <summary>
    /// Processes all child documents and relationships for a competition.
    /// This method is called for both new entities and updates to ensure
    /// child documents are always spawned if their $ref exists in the DTO.
    /// </summary>
    private async Task ProcessChildDocuments(
        ProcessDocumentCommand command,
        EspnEventCompetitionDto dto,
        Competition competition)
    {
        var raiseEvents = false;

        // Special cases that return bool for event tracking
        raiseEvents = raiseEvents || await ProcessSituation(command, dto, competition);
        raiseEvents = raiseEvents || await ProcessStatus(command, dto, competition);
        
        // All the simple $ref child documents - one line each using the generic helper
        await ProcessChildDocumentRef(command, dto.Odds, competition, DocumentType.EventCompetitionOdds);
        await ProcessChildDocumentRef(command, dto.Broadcasts, competition, DocumentType.EventCompetitionBroadcast);
        await ProcessChildDocumentRef(command, dto.Details, competition, DocumentType.EventCompetitionPlay);
        await ProcessChildDocumentRef(command, dto.Leaders, competition, DocumentType.EventCompetitionLeaders);
        await ProcessChildDocumentRef(command, dto.Predictor, competition, DocumentType.EventCompetitionPrediction);
        await ProcessChildDocumentRef(command, dto.Probabilities, competition, DocumentType.EventCompetitionProbability);
        await ProcessChildDocumentRef(command, dto.PowerIndexes, competition, DocumentType.EventCompetitionPowerIndex);
        await ProcessChildDocumentRef(command, dto.Drives, competition, DocumentType.EventCompetitionDrive);
        
        // Competitors (special handling for collections)
        await ProcessCompetitors(command, dto, competition);

        // Save outbox ping if any events need to be raised
        if (raiseEvents)
        {
            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();
        }
    }
}