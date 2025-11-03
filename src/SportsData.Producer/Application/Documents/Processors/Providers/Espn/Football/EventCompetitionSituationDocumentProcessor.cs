using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionSituation)]
    public class EventCompetitionSituationDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionSituationDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _bus;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionSituationDocumentProcessor(
            ILogger<EventCompetitionSituationDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus bus,
            IGenerateExternalRefIdentities externalRefIdentityGenerator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
                   {
                       ["CorrelationId"] = command.CorrelationId
                   }))
            {
                try
                {
                    await ProcessInternal(command);
                }
                catch (ExternalDocumentNotSourcedException retryEx)
                {
                    _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                    var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                    await _bus.Publish(docCreated);
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
            var dto = command.Document.FromJson<EspnEventCompetitionSituationDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize EspnEventCompetitionSituationDto. {@Command}", command);
                return;
            }

            if (dto is { Down: 0, Distance: 0, YardLine: 0 })
            {
                _logger.LogInformation("Situation has no data (down, distance, yardLine all zero). Skipping.");
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var competitionId))
            {
                _logger.LogError("ParentId must be a valid Guid for CompetitionId");
                return;
            }

            Guid? lastPlayId = null;

            if (dto.LastPlay is not null && !string.IsNullOrEmpty(dto.LastPlay.Ref.OriginalString))
            {
                var lastPlayIdentity = _externalRefIdentityGenerator.Generate(dto.LastPlay.Ref);

                var lastPlay = await _dataContext.CompetitionPlays
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.Id == lastPlayIdentity.CanonicalId);

                if (lastPlay == null)
                {
                    // request the play to be sourced
                    await _bus.Publish(new DocumentRequested(
                        Id: lastPlayIdentity.UrlHash,
                        ParentId: competitionId.ToString(),
                        Uri: dto.LastPlay.Ref,
                        Sport: command.Sport,
                        SeasonYear: command.Season,
                        DocumentType: DocumentType.EventCompetitionPlay,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.EventCompetitionSituationDocumentProcessor
                    ));

                    await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                    await _dataContext.SaveChangesAsync();

                    throw new ExternalDocumentNotSourcedException($"Play {dto.LastPlay.Ref} not found. Will retry.");
                }

                lastPlayId = lastPlay.Id;
            }

            var entity = dto.AsEntity(
                _externalRefIdentityGenerator,
                competitionId,
                lastPlayId,
                command.CorrelationId);

            var exists = await _dataContext.CompetitionSituations
                .AsNoTracking()
                .AnyAsync(x => x.Id == entity.Id);

            if (exists)
            {
                _logger.LogInformation("CompetitionSituation already exists with Id: {Id}", entity.Id);
                return;
            }

            await _dataContext.CompetitionSituations.AddAsync(entity);
            await _dataContext.SaveChangesAsync();
        }
    }
}
