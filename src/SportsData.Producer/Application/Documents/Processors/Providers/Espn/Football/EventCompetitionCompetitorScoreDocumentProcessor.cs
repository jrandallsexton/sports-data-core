using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorScore)]
public class EventCompetitionCompetitorScoreDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly DocumentProcessingConfig _config;

    public EventCompetitionCompetitorScoreDocumentProcessor(
        ILogger<EventCompetitionCompetitorScoreDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus bus,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        DocumentProcessingConfig config)
        : base(logger, dataContext, bus, externalRefIdentityGenerator, refs)
    {
        _config = config;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorScoreDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionCompetitorScoreDto.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionCompetitorId))
        {
            _logger.LogError("ParentId must be a valid Guid for CompetitionCompetitorId. ParentId={ParentId}", command.ParentId);
            throw new InvalidOperationException("Invalid ParentId for CompetitionCompetitorId");
        }

        // Fetch the competitor with navigation properties to get Contest and FranchiseSeason info
        var competitor = await _dataContext.CompetitionCompetitors
            .Include(x => x.Competition)
                .ThenInclude(x => x.Contest)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionCompetitorId);

        if (competitor is null)
        {
            var competitionCompetitorRef = EspnUriMapper.CompetitionCompetitorScoreRefToCompetitionCompetitorRef(dto.Ref);
            var competitionCompetitorIdentity = _externalRefIdentityGenerator.Generate(competitionCompetitorRef);

            var competitionRef = EspnUriMapper.CompetitionCompetitorRefToCompetitionRef(new Uri(competitionCompetitorIdentity.CleanUrl));
            var competitionIdentity = _externalRefIdentityGenerator.Generate(competitionRef);

            if (!_config.EnableDependencyRequests)
            {
                throw new ExternalDocumentNotSourcedException(
                    $"CompetitionCompetitor {competitionCompetitorIdentity.CleanUrl} not found. Will retry when available.");
            }
            else
            {
                _logger.LogWarning("CompetitionCompetitor not found, raising DocumentRequested. CompetitorUrl={CompetitorUrl}", 
                    competitionCompetitorIdentity.CleanUrl);

                await PublishDependencyRequest<Guid>(
                    command,
                    new EspnLinkDto { Ref = new Uri(competitionCompetitorIdentity.CleanUrl) },
                    parentId: competitionIdentity.CanonicalId,
                    DocumentType.EventCompetitionCompetitor);

                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException($"CompetitionCompetitor {competitionCompetitorIdentity.CleanUrl} not found. Requested. Will retry.");
            }
        }

        // Validate navigation properties are loaded
        if (competitor.Competition is null)
        {
            _logger.LogError("Competition not loaded for CompetitionCompetitor. CompetitorId={CompetitorId}", competitionCompetitorId);
            throw new InvalidOperationException($"Competition not loaded for CompetitionCompetitor {competitionCompetitorId}");
        }

        if (competitor.Competition.Contest is null)
        {
            _logger.LogError("Contest not loaded for Competition. CompetitorId={CompetitorId}, CompetitionId={CompetitionId}", 
                competitionCompetitorId,
                competitor.Competition.Id);
            throw new InvalidOperationException($"Contest not loaded for Competition {competitor.Competition.Id}");
        }

        var scoreIdentity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var score = await _dataContext.CompetitionCompetitorScores
            .Where(x => x.Id == scoreIdentity.CanonicalId)
            .FirstOrDefaultAsync();

        if (score != null)
        {
            _logger.LogInformation("Existing CompetitorScore found. CompetitorId={CompetitorId}, ScoreId={ScoreId}", 
                competitionCompetitorId, 
                scoreIdentity.CanonicalId);

            // Check if score actually changed before updating
            bool scoreChanged = score.Value != dto.Value || score.DisplayValue != dto.DisplayValue;
            
            if (!scoreChanged)
            {
                _logger.LogInformation("Score unchanged, skipping update. CompetitorId={CompetitorId}, Value={Value}",
                    competitionCompetitorId,
                    dto.Value);
                return;
            }

            _logger.LogInformation("Score changed, updating. CompetitorId={CompetitorId}, OldValue={OldValue}, NewValue={NewValue}",
                competitionCompetitorId,
                score.Value,
                dto.Value);
            
            score.Value = dto.Value;
            score.DisplayValue = dto.DisplayValue;
            score.ModifiedBy = command.CorrelationId;
            score.ModifiedUtc = DateTime.UtcNow;

            // Publish event BEFORE SaveChangesAsync to use MassTransit outbox pattern (navigation properties validated above)
            _logger.LogInformation("Queueing CompetitorScoreUpdated event to outbox. ContestId={ContestId}, FranchiseSeasonId={FranchiseSeasonId}, Score={Score}",
                competitor.Competition!.ContestId,
                competitor.FranchiseSeasonId,
                (int)dto.Value);

            await _publishEndpoint.Publish(new CompetitorScoreUpdated(
                ContestId: competitor.Competition!.ContestId,
                FranchiseSeasonId: competitor.FranchiseSeasonId,
                Score: (int)dto.Value,
                Ref: null,
                Sport: command.Sport,
                SeasonYear: command.Season,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionCompetitorScoreDocumentProcessor
            ));

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Score update persisted and event queued. CompetitorId={CompetitorId}, Value={Value}",
                competitionCompetitorId,
                dto.Value);
        }
        else
        {
            _logger.LogInformation("Creating new CompetitorScore. CompetitorId={CompetitorId}", competitionCompetitorId);

            var entity = dto.AsEntity(
                competitionCompetitorId,
                _externalRefIdentityGenerator,
                command.SourceDataProvider,
                command.CorrelationId);

            await _dataContext.CompetitionCompetitorScores.AddAsync(entity);

            // Publish event BEFORE SaveChangesAsync to use MassTransit outbox pattern (navigation properties validated above)
            _logger.LogInformation("Queueing CompetitorScoreUpdated event to outbox. ContestId={ContestId}, FranchiseSeasonId={FranchiseSeasonId}, Score={Score}",
                competitor.Competition!.ContestId,
                competitor.FranchiseSeasonId,
                (int)dto.Value);

            await _publishEndpoint.Publish(new CompetitorScoreUpdated(
                ContestId: competitor.Competition!.ContestId,
                FranchiseSeasonId: competitor.FranchiseSeasonId,
                Score: (int)dto.Value,
                Ref: null,
                Sport: command.Sport,
                SeasonYear: command.Season,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionCompetitorScoreDocumentProcessor
            ));

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("New score persisted and event queued. CompetitorId={CompetitorId}, Value={Value}", 
                competitionCompetitorId, 
                dto.Value);
        }

        _logger.LogInformation("CompetitorScore processing completed. CompetitorId={CompetitorId}, Value={Value}", 
            competitionCompetitorId, 
            dto.Value);
    }
}