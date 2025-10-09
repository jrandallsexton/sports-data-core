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

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorScore)]
    public class EventCompetitionCompetitorScoreDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionCompetitorScoreDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _bus;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionCompetitorScoreDocumentProcessor(
            ILogger<EventCompetitionCompetitorScoreDocumentProcessor<TDataContext>> logger,
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
            var dto = command.Document.FromJson<EspnEventCompetitionCompetitorScoreDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize EspnEventCompetitionCompetitorScoreDto. {@Command}", command);
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var competitionCompetitorId))
            {
                _logger.LogError("ParentId must be a valid Guid for CompetitionCompetitorId");
                throw new InvalidOperationException("Invalid ParentId for CompetitionCompetitorId");
            }

            var exists = await _dataContext.CompetitionCompetitors
                .AsNoTracking()
                .AnyAsync(x => x.Id == competitionCompetitorId);

            if (!exists)
            {
                var competitionCompetitorRef =
                    EspnUriMapper.CompetitionCompetitorScoreRefToCompetitionCompetitorRef(dto.Ref);
                var competitionCompetitorIdentity =
                    _externalRefIdentityGenerator.Generate(competitionCompetitorRef);

                var competitionRef =
                    EspnUriMapper.CompetitionCompetitorRefToCompetitionRef(new Uri(competitionCompetitorIdentity
                        .CleanUrl));
                var competitionIdentity = _externalRefIdentityGenerator.Generate(competitionRef);

                await _bus.Publish(new DocumentRequested(
                    Id: competitionCompetitorIdentity.UrlHash,
                    ParentId: competitionIdentity.CanonicalId.ToString(),
                    Uri: new Uri(competitionCompetitorIdentity.CleanUrl),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.EventCompetitionCompetitor,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.GroupSeasonDocumentProcessor
                ));

                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new ExternalDocumentNotSourcedException($"CompetitionCompetitor {competitionCompetitorIdentity.CleanUrl} not found. Will retry.");
            }

            var entity = dto.AsEntity(
                competitionCompetitorId,
                _externalRefIdentityGenerator,
                command.SourceDataProvider,
                command.CorrelationId);

            await _dataContext.CompetitionCompetitorScores.AddAsync(entity);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Persisted score for Competitor {Id}", competitionCompetitorId);
        }
    }
}
