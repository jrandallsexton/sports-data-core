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

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamSeason)]
public class TeamSeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly DocumentProcessingConfig _config;

    public TeamSeasonDocumentProcessor(
        ILogger<TeamSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
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

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Processing TeamSeasonDocument. DocumentType={DocumentType}, UrlHash={UrlHash}",
                command.DocumentType,
                command.UrlHash);
            try
            {
                await ProcessInternal(command);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@SafeCommand}", command.ToSafeLogObject());
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
                    Ref: null,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.Franchise,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.TeamSeasonDocumentProcessor
                ));
                
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

        await _dataContext.SaveChangesAsync();
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
                    Ref: null,
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.GroupSeason,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.TeamSeasonDocumentProcessor
                ));
                
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
            null,
            command.Sport,
            command.Season,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));
    }

    private async Task ProcessDependents(
        FranchiseSeason canonicalEntity,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        // Logos - special handling (uses EventFactory and PublishBatch)
        await ProcessLogos(canonicalEntity.Id, dto, command);

        // All other child documents - one line each using base class helper
        await PublishChildDocumentRequest(command, dto.Record, canonicalEntity.Id, DocumentType.TeamSeasonRecord, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Ranks, canonicalEntity.Id, DocumentType.TeamSeasonRank, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Statistics, canonicalEntity.Id, DocumentType.TeamSeasonStatistics, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Athletes, canonicalEntity.Id, DocumentType.AthleteSeason, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Leaders, canonicalEntity.Id, DocumentType.TeamSeasonLeaders, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Injuries, canonicalEntity.Id, DocumentType.TeamSeasonInjuries, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.AgainstTheSpreadRecords, canonicalEntity.Id, DocumentType.TeamSeasonRecordAts, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Awards, canonicalEntity.Id, DocumentType.TeamSeasonAward, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Projection, canonicalEntity.Id, DocumentType.TeamSeasonProjection, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Events, canonicalEntity.Id, DocumentType.Event, CausationId.Producer.TeamSeasonDocumentProcessor);
        await PublishChildDocumentRequest(command, dto.Coaches, canonicalEntity.Id, DocumentType.TeamSeasonCoach, CausationId.Producer.TeamSeasonDocumentProcessor);

        // TODO: MED: Request sourcing of team season notes (data not available when following link)
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
        // Request updated child documents based on inclusion filter
        if (ShouldSpawn(DocumentType.AthleteSeason, command))
            await PublishChildDocumentRequest(command, dto.Athletes, existing.Id, DocumentType.AthleteSeason, CausationId.Producer.TeamSeasonDocumentProcessor);

        if (ShouldSpawn(DocumentType.Event, command))
            await PublishChildDocumentRequest(command, dto.Events, existing.Id, DocumentType.Event, CausationId.Producer.TeamSeasonDocumentProcessor);

        if (ShouldSpawn(DocumentType.TeamSeasonLeaders, command))
            await PublishChildDocumentRequest(command, dto.Leaders, existing.Id, DocumentType.TeamSeasonLeaders, CausationId.Producer.TeamSeasonDocumentProcessor);

        if (ShouldSpawn(DocumentType.TeamSeasonRank, command))
            await PublishChildDocumentRequest(command, dto.Ranks, existing.Id, DocumentType.TeamSeasonRank, CausationId.Producer.TeamSeasonDocumentProcessor);

        if (ShouldSpawn(DocumentType.TeamSeasonStatistics, command))
            await PublishChildDocumentRequest(command, dto.Statistics, existing.Id, DocumentType.TeamSeasonStatistics, CausationId.Producer.TeamSeasonDocumentProcessor);
    }
}
