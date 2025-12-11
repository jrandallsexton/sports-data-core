using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeason)]
public class TeamSeasonDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<TeamSeasonDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
    private readonly DocumentProcessingConfig _config;

    public TeamSeasonDocumentProcessor(
        ILogger<TeamSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        DocumentProcessingConfig config)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
        _config = config;
    }

    /// <summary>
    /// Determines if a linked document of the specified type should be spawned,
    /// based on the inclusion filter in the command.
    /// </summary>
    /// <param name="documentType">The type of linked document to check</param>
    /// <param name="command">The processing command containing the optional inclusion filter</param>
    /// <returns>True if the document should be spawned; false otherwise</returns>
    private bool ShouldSpawn(DocumentType documentType, ProcessDocumentCommand command)
    {
        // If no inclusion filter is specified, spawn all documents (default behavior)
        if (command.IncludeLinkedDocumentTypes == null || command.IncludeLinkedDocumentTypes.Count == 0)
        {
            return true;
        }

        // If inclusion filter is specified, only spawn if the type is in the list
        var shouldSpawn = command.IncludeLinkedDocumentTypes.Contains(documentType);

        if (!shouldSpawn)
        {
            _logger.LogInformation(
                "Skipping spawn of {DocumentType} due to inclusion filter. Allowed types: {AllowedTypes}",
                documentType,
                string.Join(", ", command.IncludeLinkedDocumentTypes));
        }

        return shouldSpawn;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Processing EventDocument with {@Command}", command);
            try
            {
                await ProcessInternal(command);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }

    public async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalProviderDto = command.Document.FromJson<EspnTeamSeasonDto>();

        if (externalProviderDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnTeamSeasonDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
        {
            _logger.LogError("EspnTeamSeasonDto Ref is null or empty. {@Command}", command);
            return;
        }

        if (command.Season is null)
        {
            _logger.LogError("Season year must be provided.");
            return;
        }

        var franchiseIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Franchise.Ref);

        var franchise = await _dataContext.Franchises
            .Include(f => f.Seasons)
            .FirstOrDefaultAsync(x => x.Id == franchiseIdentity.CanonicalId);

        if (franchise is null)
        {
            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.Franchise,
                    nameof(TeamSeasonDocumentProcessor<TDataContext>),
                    franchiseIdentity.CleanUrl);
                throw new ExternalDocumentNotSourcedException(
                    $"Franchise {franchiseIdentity.CleanUrl} not found. Will retry when available.");
            }
            else
            {
                _logger.LogWarning(
                    "Franchise not found. Raising DocumentRequested (override mode). {@Identity}",
                    franchiseIdentity);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: franchiseIdentity.UrlHash,
                    ParentId: null,
                    Uri: new Uri(franchiseIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.Franchise,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.TeamSeasonDocumentProcessor
                ));
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"Franchise {externalProviderDto.Franchise.Ref} not found. Will retry.");
            }
        }

        var franchiseSeasonIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Ref);
        var existingSeason = franchise.Seasons.FirstOrDefault(s => s.Id == franchiseSeasonIdentity.CanonicalId);

        if (existingSeason is not null)
        {
            await ProcessUpdateEntity(existingSeason, externalProviderDto, command);
        }
        else
        {
            await ProcessNewEntity(franchise, command.Season.Value, externalProviderDto, command);
        }
    }

    private async Task ProcessDependencies(
        Franchise franchise,
        FranchiseSeason canonicalEntity,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (string.IsNullOrEmpty(canonicalEntity.ColorCodeHex))
        {
            canonicalEntity.ColorCodeHex = franchise.ColorCodeHex;
        }

        if (string.IsNullOrEmpty(canonicalEntity.Abbreviation))
        {
            canonicalEntity.Abbreviation = franchise.Abbreviation ?? "UNK";
        }

        // Resolve VenueId via SourceUrlHash
        canonicalEntity.VenueId = await _dataContext.ResolveIdAsync<
            Venue, VenueExternalId>(
            dto.Venue,
            command.SourceDataProvider,
            () => _dataContext.Venues,
            externalIdsNav: "ExternalIds",
            key: v => v.Id);

        if (dto.Groups?.Ref is null)
        {
            _logger.LogInformation("No group reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Resolve GroupId via SourceUrlHash
        canonicalEntity.GroupSeasonId = await _dataContext.ResolveIdAsync<
            GroupSeason, GroupSeasonExternalId>(
            dto.Groups,
            command.SourceDataProvider,
            () => _dataContext.GroupSeasons,
            externalIdsNav: "ExternalIds",
            key: gs => gs.Id);

        if (canonicalEntity.GroupSeasonId is null)
        {
            if (!_config.EnableDependencyRequests)
            {
                _logger.LogWarning(
                    "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                    DocumentType.GroupSeason,
                    nameof(TeamSeasonDocumentProcessor<TDataContext>),
                    dto.Groups.Ref);
                throw new ExternalDocumentNotSourcedException(
                    $"GroupSeason {dto.Groups.Ref} not found. Will retry when available.");
            }
            else
            {
                _logger.LogWarning(
                    "GroupSeason not found. Raising DocumentRequested (override mode). {Ref}",
                    dto.Groups.Ref);

                var groupSeasonIdentity = _externalRefIdentityGenerator.Generate(dto.Groups.Ref);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: groupSeasonIdentity.UrlHash,
                    ParentId: null,
                    Uri: new Uri(groupSeasonIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.GroupSeason,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.TeamSeasonDocumentProcessor
                ));

                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException(
                    $"GroupSeason {dto.Groups.Ref} not found. Will retry.");
            }
        }
    }

    private async Task ProcessNewEntity(
        Franchise franchise,
        int seasonYear,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        var canonicalEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            franchise.Id,
            seasonYear,
            command.CorrelationId);

        // TODO: This might be a bug b/c it does not check for dependency errors/issues
        await ProcessDependencies(franchise, canonicalEntity, dto, command);

        await ProcessDependents(canonicalEntity, dto, command);

        await _dataContext.FranchiseSeasons.AddAsync(canonicalEntity);

        await _publishEndpoint.Publish(new FranchiseSeasonCreated(
            canonicalEntity.ToCanonicalModel(),
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessDependents(
        FranchiseSeason canonicalEntity,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        // logos
        await ProcessLogos(canonicalEntity.Id, dto, command);

        // Wins/Losses/PtsFor/PtsAgainst
        await ProcessRecord(canonicalEntity.Id, dto, command);

        // rankings
        await ProcessRanks(canonicalEntity.Id, dto, command);

        // stats
        await ProcessStatistics(canonicalEntity.Id, dto, command);

        // athletes
        await ProcessAthletes(canonicalEntity.Id, dto, command);

        // leaders
        await ProcessLeaders(canonicalEntity.Id, dto, command);

        // injuries
        await ProcessInjuries(canonicalEntity.Id, dto, command);

        // TODO: MED: Request sourcing of team season notes (data not available when following link)

        // Process record ATS (Against The Spread)
        await ProcessRecordAts(canonicalEntity.Id, dto, command);

        // Process awards
        await ProcessAwards(canonicalEntity.Id, dto, command);

        // Process projection
        await ProcessProjection(canonicalEntity.Id, dto, command);

        // Process events (schedule)
        await ProcessEvents(canonicalEntity.Id, dto, command);

        // Process coaches
        await ProcessCoaches(canonicalEntity.Id, dto, command);
    }

    private async Task ProcessRanks(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Ranks?.Ref is null)
        {
            _logger.LogInformation("No ranking reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season ranks
        await _publishEndpoint.Publish(new DocumentRequested(
            Guid.NewGuid().ToString(),
            franchiseSeasonId.ToString(),
            dto.Ranks.Ref.ToCleanUri(),
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonRank,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessProjection(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Projection?.Ref is null)
        {
            _logger.LogInformation("No projection reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season projection
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Projection.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.Projection.Ref.ToCleanUri(),
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonProjection,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessEvents(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Events?.Ref is null)
        {
            _logger.LogWarning("No events reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season events (schedule)
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Events.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.Events.Ref,
            command.Sport,
            command.Season,
            DocumentType.Event,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessCoaches(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Coaches?.Ref is null)
        {
            _logger.LogInformation("No coaches reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season coaches
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Coaches.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.Coaches.Ref,
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonCoach,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessAwards(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Awards?.Ref is null)
        {
            _logger.LogInformation("No awards reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season awards
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Awards.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.Awards.Ref,
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonAward,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessRecordAts(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.AgainstTheSpreadRecords?.Ref is null)
        {
            _logger.LogInformation("No record reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season record ATS (Against The Spread)
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.AgainstTheSpreadRecords.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.AgainstTheSpreadRecords.Ref,
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonRecordAts,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessInjuries(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Injuries?.Ref is null)
        {
            _logger.LogInformation("No injuries reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season injuries
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Injuries.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.Injuries.Ref,
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonInjuries,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessAthletes(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Athletes?.Ref is null)
        {
            _logger.LogInformation("No athletes reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season athletes
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Athletes.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.Athletes.Ref,
            command.Sport,
            command.Season,
            DocumentType.AthleteSeason,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessLeaders(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Leaders?.Ref is null)
        {
            _logger.LogInformation("No leaders reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season leaders
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Leaders.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.Leaders.Ref,
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonLeaders,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessStatistics(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Statistics?.Ref is null)
        {
            _logger.LogInformation("No statistics reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season statistics
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Statistics.Ref.ToCleanUrl(),
            franchiseSeasonId.ToString(),
            dto.Statistics.Ref,
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonStatistics,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessRecord(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Record?.Ref is null)
        {
            _logger.LogInformation("No record found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Request sourcing of team season record
        var recordIdentity = _externalRefIdentityGenerator.Generate(dto.Record.Ref);

        await _publishEndpoint.Publish(new DocumentRequested(
            recordIdentity.CanonicalId.ToString(),
            franchiseSeasonId.ToString(),
            new Uri(recordIdentity.CleanUrl),
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonRecord,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessLogos(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Logos is null || dto.Logos.Count == 0)
        {
            _logger.LogInformation("No logos found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        var imageEvents = EventFactory.CreateProcessImageRequests(
            dto.Logos,
            franchiseSeasonId,
            command.Sport,
            command.Season,
            command.DocumentType,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor);

        if (imageEvents.Count > 0)
        {
            _logger.LogInformation("Publishing {Count} image requests for TeamSeason {Season}", imageEvents.Count, command.Season);
            await _publishEndpoint.PublishBatch(imageEvents);
        }
    }

    private async Task ProcessUpdateEntity(
        FranchiseSeason existing,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        // request updated statistics
        if (ShouldSpawn(DocumentType.AthleteSeason, command))
            await ProcessAthletes(existing.Id, dto, command);

        if (ShouldSpawn(DocumentType.Event, command))
            await ProcessEvents(existing.Id, dto, command);

        if (ShouldSpawn(DocumentType.TeamSeasonLeaders, command))
            await ProcessLeaders(existing.Id, dto, command);

        if (ShouldSpawn(DocumentType.TeamSeasonRank, command))
            await ProcessRanks(existing.Id, dto, command);

        if (ShouldSpawn(DocumentType.TeamSeasonStatistics, command))
            await ProcessStatistics(existing.Id, dto, command);

        await _dataContext.OutboxPings.AddAsync(new OutboxPing());
        await _dataContext.SaveChangesAsync();
    }
}
