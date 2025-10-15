using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
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
            _logger.LogError("Competition not found for {CompetitionId}", competitionId);

            var competitionRef = EspnUriMapper.CompetitionCompetitorRefToCompetitionRef(dto.Ref);
            var competitionIdentity = _externalRefIdentityGenerator.Generate(competitionRef);

            var contestRef = EspnUriMapper.CompetitionRefToContestRef(competitionRef);
            var contestIdentity = _externalRefIdentityGenerator.Generate(contestRef);

            // raise an event to source the competition competitor
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: competitionIdentity.UrlHash,
                ParentId: contestIdentity.CanonicalId.ToString(),
                Uri: competitionRef,
                Sport: command.Sport,
                SeasonYear: command.Season,
                DocumentType: DocumentType.EventCompetition,
                SourceDataProvider: command.SourceDataProvider,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionCompetitorLineScoreDocumentProcessor
            ));

            throw new ExternalDocumentNotSourcedException($"Competition with ID {competitionId} does not exist.");
        }

        var franchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            dto.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

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
        await _dataContext.SaveChangesAsync();

        await ProcessScores(canonicalEntity.Id, dto, command);

        await ProcessLineScores(canonicalEntity.Id, dto, command);

        // TODO: ProcessRoster

        // TODO: ProcessStatistics

        // TODO: ProcessLeaders

        // TODO: ProcessRecord

        // TODO: ProcessRanks
    }

    private async Task ProcessScores(
        Guid competitionCompetitorId,
        EspnEventCompetitionCompetitorDto externalProviderDto,
        ProcessDocumentCommand command)
    {
        if (externalProviderDto.Score?.Ref is null)
            return;

        var competitorScoreIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Score.Ref);

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: competitorScoreIdentity.UrlHash,
            ParentId: competitionCompetitorId.ToString(),
            Uri: new Uri(competitorScoreIdentity.CleanUrl),
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

        var lineScoresIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Linescores.Ref);

        await _publishEndpoint.Publish(new DocumentRequested(
            Id: lineScoresIdentity.UrlHash,
            ParentId: competitionCompetitorId.ToString(),
            Uri: new Uri(lineScoresIdentity.CleanUrl),
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
