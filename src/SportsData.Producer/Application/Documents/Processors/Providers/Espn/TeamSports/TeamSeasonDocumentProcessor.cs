﻿using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
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

    public TeamSeasonDocumentProcessor(
        ILogger<TeamSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
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

        if (franchiseIdentity is null)
        {
            _logger.LogError($"Failed to generate franchise hash for {externalProviderDto.Franchise.Ref}");
            throw new InvalidOperationException($"Franchise hash generation failed for {externalProviderDto.Franchise.Ref}");
        }

        var franchise = await _dataContext.Franchises
            .Include(f => f.Seasons)
            .FirstOrDefaultAsync(x => x.Id == franchiseIdentity.CanonicalId);

        if (franchise is null)
        {
            _logger.LogWarning("Franchise not found. {@Identity}", franchiseIdentity);

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

            throw new ExternalDocumentNotSourcedException($"Franchise {externalProviderDto.Franchise.Ref} not found. Will retry.");
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
        canonicalEntity.VenueId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Venue,
            command.SourceDataProvider,
            () => _dataContext.Venues.Include(x => x.ExternalIds).AsNoTracking(),
            _logger);

        if (dto.Groups?.Ref is null)
        {
            _logger.LogInformation("No group reference found in the DTO for TeamSeason {Season}", command.Season);
            return;
        }

        // Resolve GroupId via SourceUrlHash
        canonicalEntity.GroupSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Groups,
            command.SourceDataProvider,
            () => _dataContext.GroupSeasons.Include(x => x.ExternalIds).AsNoTracking(),
            _logger);

        if (canonicalEntity.GroupSeasonId is null)
        {
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: Guid.NewGuid().ToString(),
                ParentId: null,
                Uri: dto.Groups.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.GroupSeason,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.TeamSeasonDocumentProcessor
            ));

            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();

            throw new ExternalDocumentNotSourcedException($"GroupSeason {dto.Groups.Ref} not found. Will retry.");
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
        var recordUri = dto.Record.Ref;
        var recordId = recordUri.Segments.Last().TrimEnd('/');

        await _publishEndpoint.Publish(new DocumentRequested(
            recordId,
            franchiseSeasonId.ToString(),
            recordUri,
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
        var saveChanges = false;

        // request updated statistics
        if (dto.Statistics?.Ref is not null)
        {
            var identity = _externalRefIdentityGenerator.Generate(dto.Statistics.Ref);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: identity.UrlHash,
                ParentId: existing.Id.ToString(),
                Uri: new Uri(identity.CleanUrl),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.TeamSeasonStatistics,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.TeamSeasonDocumentProcessor
            ));
            saveChanges = true;
            _logger.LogInformation($"{nameof(TeamSeasonDocumentProcessor<TDataContext>)} raising DocumentRequested for Statistics");
        }

        if (dto.Ranks?.Ref is not null)
        {
            var identity = _externalRefIdentityGenerator.Generate(dto.Ranks.Ref);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: identity.UrlHash,
                ParentId: existing.Id.ToString(),
                Uri: new Uri(identity.CleanUrl),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.TeamSeasonRank,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.TeamSeasonDocumentProcessor
            ));
            saveChanges = true;
            _logger.LogInformation($"{nameof(TeamSeasonDocumentProcessor<TDataContext>)} raising DocumentRequested for Ranks");
        }

        if (dto.Leaders?.Ref is not null)
        {
            var identity = _externalRefIdentityGenerator.Generate(dto.Leaders.Ref);

            await _publishEndpoint.Publish(new DocumentRequested(
                Id: identity.UrlHash,
                ParentId: existing.Id.ToString(),
                Uri: new Uri(identity.CleanUrl),
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.TeamSeasonLeaders,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.TeamSeasonDocumentProcessor
            ));
            saveChanges = true;
            _logger.LogInformation($"{nameof(TeamSeasonDocumentProcessor<TDataContext>)} raising DocumentRequested for Leaders");
        }

        if (saveChanges)
        {
            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();
        }
        else
        {
            _logger.LogWarning($"{nameof(TeamSeasonDocumentProcessor<TDataContext>)} raised zero events.");
        }
    }
}
