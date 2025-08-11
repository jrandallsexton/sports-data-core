using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorLineScore)]
    public class EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _bus;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionCompetitorLineScoreDocumentProcessor(
            ILogger<EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IPublishEndpoint bus,
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
                _logger.LogInformation("Processing LineScores with {@Command}", command);
                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var dto = command.Document.FromJson<EspnEventCompetitionCompetitorLineScoreDto>();

            if (dto is null)
            {
                _logger.LogWarning("No line score found to process. {@Command}", command);
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
                _logger.LogError("CompetitionCompetitor not found for Id: {Id}", competitionCompetitorId);
                throw new InvalidOperationException($"No CompetitionCompetitor exists with ID: {competitionCompetitorId}");
            }

            var entity = dto.AsEntity(
                competitionCompetitorId,
                _externalRefIdentityGenerator,
                command.SourceDataProvider,
                command.CorrelationId);

            await _dataContext.CompetitionCompetitorLineScores.AddAsync(entity);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Persisted line score for Competitor {Id}", competitionCompetitorId);
        }

    }
}
