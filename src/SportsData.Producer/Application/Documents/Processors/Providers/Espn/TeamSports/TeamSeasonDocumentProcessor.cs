using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.TeamBySeason)]
public class TeamSeasonDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<TeamSeasonDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public TeamSeasonDocumentProcessor(
        ILogger<TeamSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IPublishEndpoint publishEndpoint)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            _logger.LogInformation("Began processing TeamSeason with {@Command}", command);
            await ProcessInternal(command);
        }
    }

    public async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalProviderDto = command.Document.FromJson<EspnTeamSeasonDto>();

        if (externalProviderDto is null)
        {
            _logger.LogError($"Error deserializing {command.DocumentType}");
            throw new InvalidOperationException($"Deserialization returned null for EspnVenueDto. CorrelationId: {command.CorrelationId}");
        }

        if (command.Season is null)
            throw new InvalidOperationException("Season year must be provided.");

        var franchise = await _dataContext.Franchises
            .Include(f => f.Seasons)
            .FirstOrDefaultAsync(f => f.ExternalIds.Any(id => id.Value == externalProviderDto.Id.ToString() &&
                                                              id.Provider == command.SourceDataProvider));

        if (franchise is null)
            throw new InvalidOperationException($"Franchise {externalProviderDto.Id} not found.");

        var franchiseSeasonId = DeterministicGuid.Combine(franchise.Id, command.Season.Value);
        var existingSeason = franchise.Seasons.FirstOrDefault(s => s.Id == franchiseSeasonId);

        if (existingSeason is not null)
        {
            await ProcessUpdateEntity(existingSeason, externalProviderDto, command);
        }
        else
        {
            await ProcessNewEntity(franchise.Id, franchiseSeasonId, externalProviderDto, command);
        }

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessNewEntity(
        Guid franchiseId,
        Guid seasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        var franchiseSeason = dto.AsEntity(franchiseId, seasonId, command.Season!.Value, command.CorrelationId);

        // Resolve VenueId via SourceUrlHash
        franchiseSeason.VenueId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Venue, command.SourceDataProvider, () => _dataContext.Venues, _logger);

        // Resolve GroupId via SourceUrlHash
        franchiseSeason.GroupId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Groups, command.SourceDataProvider, () => _dataContext.Groups, _logger);

        // Map EspnCoachSeasonRecordDto (Wins/Losses/PtsFor/PtsAgainst) from dto.EspnCoachSeasonRecordDto
        await ProcessRecord(franchiseSeason.Id, dto, command);

        // TODO: Extract Ranks from dto.Ranks
        // Consider creating a new FranchiseSeasonRank entity to store weekly ranks per source

        // Request sourcing of team season statistics
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Statistics.Ref.ToCleanUrl(),
            franchiseSeason.Id.ToString(),
            dto.Statistics.Ref,
            command.Sport,
            command.Season,
            DocumentType.TeamSeasonStatistics,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));

        // TODO: Request sourcing of team season leaders

        // TODO: Request sourcing of team season injuries

        // TODO: Request sourcing of team season notes

        // TODO: Handle Links (e.g., schedule, roster, stats pages)
        // These can be stored as ExternalLinks tied to FranchiseSeason, if valuable for downstream use

        // Request sourcing of season events (schedule)
        await _publishEndpoint.Publish(new DocumentRequested(
            dto.Events.Ref.ToCleanUrl(),
            franchiseSeason.Id.ToString(),
            dto.Events.Ref,
            command.Sport,
            command.Season,
            DocumentType.Event,
            command.SourceDataProvider,
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));

        // TODO: Track Source URL or ESPN Ref for traceability/debugging
        // If not already persisted, consider saving the original document URL hash or ref

        await _dataContext.FranchiseSeasons.AddAsync(franchiseSeason);

        // Handle logos
        var imageEvents = EventFactory.CreateProcessImageRequests(
            dto.Logos,
            seasonId,
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

        await _publishEndpoint.Publish(new FranchiseSeasonCreated(
            franchiseSeason.ToCanonicalModel(),
            command.CorrelationId,
            CausationId.Producer.TeamSeasonDocumentProcessor));

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessRecord(
        Guid franchiseSeasonId,
        EspnTeamSeasonDto dto,
        ProcessDocumentCommand command)
    {
        if (dto.Record?.Ref is not null)
        {
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
    }

    private async Task ProcessUpdateEntity(FranchiseSeason existing, EspnTeamSeasonDto dto, ProcessDocumentCommand command)
    {
        await Task.Delay(100);

        // TODO: Compare and update if necessary
        // For now, log and skip
        _logger.LogInformation("FranchiseSeason {Id} already exists. Skipping update.", existing.Id);
    }
}
