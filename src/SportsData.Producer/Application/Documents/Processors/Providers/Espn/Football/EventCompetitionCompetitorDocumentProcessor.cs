using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitor)]
public class EventCompetitionCompetitorDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<EventCompetitionCompetitorDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public EventCompetitionCompetitorDocumentProcessor(
        ILogger<EventCompetitionCompetitorDocumentProcessor<TDataContext>> logger,
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
            _logger.LogInformation("Processing EventCompetitionCompetitorDocument with {@Command}", command);
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
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnEventCompetitionCompetitorDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrWhiteSpace(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionCompetitorDto Ref is null. {@Command}", command);
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command must have a SeasonYear defined");
            return;
        }

        if (string.IsNullOrWhiteSpace(command.ParentId))
        {
            _logger.LogError("Command must have a ParentId defined for the CompetitionId");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("CompetitionId could not be parsed");
            return;
        }

        var competitionExists = await _dataContext.Competitions
            .AsNoTracking()
            .AnyAsync(x => x.Id == competitionId);

        if (!competitionExists)
        {
            // TODO: Publish a DocumentRequested event for the Competition
            // Problem with this is that we do not know the parentId which is the ContestId. ugh.
            // In the meantime, just throw an exception, allow Hangfire to retry and hopefully the Competition gets sourced prior to then
            _logger.LogError("Competition not found for {CompetitionId}", competitionId);
            throw new InvalidOperationException($"Competition with ID {competitionId} does not exist.");
        }

        var franchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
            _logger);

        if (franchiseSeasonId is null)
        {
            _logger.LogError("FranchiseSeason could not be resolved from DTO reference: {@DtoRef}", dto.Team?.Ref);
            throw new InvalidOperationException("FranchiseSeason could not be resolved from DTO reference.");
        }

        var entity = await _dataContext.CompetitionCompetitors
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x =>
                x.ExternalIds.Any(z => z.SourceUrlHash == command.UrlHash &&
                                       z.Provider == command.SourceDataProvider));

        if (entity is null)
        {
            await ProcessNewEntity(command, dto, competitionId, franchiseSeasonId.Value);
        }
        else
        {
            await ProcessUpdate(command, dto, entity);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId)
    {
        var canonicalEntity = dto.AsEntity(
            competitionId,
            franchiseSeasonId,
            _externalRefIdentityGenerator,
            command.CorrelationId);

        await _dataContext.CompetitionCompetitors.AddAsync(canonicalEntity);

        await ProcessScores(canonicalEntity.Id, dto, command);

        await ProcessLineScores(canonicalEntity.Id, dto, command);

        // TODO: ProcessRoster

        // TODO: ProcessStatistics

        // TODO: ProcessLeaders

        // TODO: ProcessRecord

        // TODO: ProcessRanks

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessScores(
        Guid competitionCompetitorId,
        EspnEventCompetitionCompetitorDto externalProviderDto,
        ProcessDocumentCommand command)
    {
        if (externalProviderDto.Score?.Ref is null)
            return;

        var identity = _externalRefIdentityGenerator.Generate(externalProviderDto.Score.Ref);

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: identity.CanonicalId.ToString(),
            ParentId: competitionCompetitorId.ToString(),
            Uri: externalProviderDto.Score.Ref.ToCleanUri(),
            Sport: Sport.FootballNcaa,
            SeasonYear: command.Season,
            DocumentType: DocumentType.EventCompetitionCompetitorScore,
            SourceDataProvider: SourceDataProvider.Espn,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionCompetitorDocumentProcessor
        ));
    }

    private async Task ProcessLineScores(
        Guid competitionCompetitorId,
        EspnEventCompetitionCompetitorDto externalProviderDto,
        ProcessDocumentCommand command)
    {
        if (externalProviderDto.Linescores?.Ref is null)
            return;

        var identity = _externalRefIdentityGenerator.Generate(externalProviderDto.Linescores.Ref);

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: identity.CanonicalId.ToString(),
            ParentId: competitionCompetitorId.ToString(),
            Uri: externalProviderDto.Linescores.Ref.ToCleanUri(),
            Sport: Sport.FootballNcaa,
            SeasonYear: command.Season,
            DocumentType: DocumentType.EventCompetitionCompetitorLineScore,
            SourceDataProvider: SourceDataProvider.Espn,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionCompetitorDocumentProcessor
        ));
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionCompetitorDto dto,
        CompetitionCompetitor entity)
    {
        // TODO: Implement update logic if needed
        await Task.CompletedTask;
    }
}
