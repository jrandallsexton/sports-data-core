using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
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
        private readonly IEventBus _bus;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionCompetitorLineScoreDocumentProcessor(
            ILogger<EventCompetitionCompetitorLineScoreDocumentProcessor<TDataContext>> logger,
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
                _logger.LogInformation("Processing LineScores with {@Command}", command);
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

            var identity = _externalRefIdentityGenerator.Generate(dto.Ref);
            var entry = await _dataContext.CompetitionCompetitorLineScores
                .AsTracking()
                .FirstOrDefaultAsync(x => x.Id == identity.CanonicalId);

            if (entry is not null)
            {
                entry.Value = dto.Value;
                entry.DisplayValue = dto.DisplayValue;
                entry.Period = dto.Period;
                entry.SourceId = dto.Source?.Id ?? string.Empty;
                entry.SourceDescription = dto.Source?.Description ?? string.Empty;
                entry.SourceState = dto.Source?.State;
                entry.ModifiedUtc = DateTime.UtcNow;
                entry.ModifiedBy = command.CorrelationId;

                _logger.LogInformation("Updated existing line score for Competitor {Id}, Period {Period}", competitionCompetitorId, dto.Period);
            }
            else
            {
                var entity = dto.AsEntity(
                    competitionCompetitorId,
                    _externalRefIdentityGenerator,
                    command.SourceDataProvider,
                    command.CorrelationId);

                await _dataContext.CompetitionCompetitorLineScores.AddAsync(entity);

                _logger.LogInformation("Inserted new line score for Competitor {Id}, Period {Period}", competitionCompetitorId, dto.Period);
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}