using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteSeasonStatistics)]
    public class AthleteSeasonStatisticsDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<AthleteSeasonStatisticsDocumentProcessor> _logger;
        private readonly FootballDataContext _dataContext;
        private readonly IEventBus _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public AthleteSeasonStatisticsDocumentProcessor(
            ILogger<AthleteSeasonStatisticsDocumentProcessor> logger,
            FootballDataContext dataContext,
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
                _logger.LogInformation("Processing AthleteSeasonStatistics with {@Command}", command);
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
            if (!Guid.TryParse(command.ParentId, out var athleteSeasonId))
            {
                _logger.LogError("Invalid ParentId: {ParentId}", command.ParentId);
                return;
            }

            var athleteSeason = await _dataContext.AthleteSeasons
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == athleteSeasonId);

            if (athleteSeason is null)
            {
                _logger.LogError("AthleteSeason not found: {AthleteSeasonId}", athleteSeasonId);
                return;
            }

            var dto = command.Document.FromJson<EspnAthleteSeasonStatisticsDto>();

            if (dto is null)
            {
                _logger.LogError("DTO is null for AthleteSeasonStatistics processing. ParentId: {ParentId}", command.ParentId);
                return;
            }

            if (dto.Ref == null)
            {
                _logger.LogError("AthleteSeasonStatistics DTO missing $ref. ParentId: {ParentId}", command.ParentId);
                return;
            }

            var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

            // ESPN replaces statistics wholesale, so remove existing if present
            var existing = await _dataContext.AthleteSeasonStatistics
                .Include(x => x.Categories)
                    .ThenInclude(c => c.Stats)
                .AsSplitQuery()
                .FirstOrDefaultAsync(r => r.Id == identity.CanonicalId);

            if (existing is not null)
            {
                _logger.LogInformation("Removing existing AthleteSeasonStatistic {Id} for replacement", existing.Id);
                _dataContext.AthleteSeasonStatistics.Remove(existing);
                await _dataContext.SaveChangesAsync();
            }

            var entity = dto.AsEntity(
                _externalRefIdentityGenerator,
                athleteSeasonId,
                command.CorrelationId);

            await _dataContext.AthleteSeasonStatistics.AddAsync(entity);
            await _dataContext.OutboxPings.AddAsync(new OutboxPing());
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Successfully processed AthleteSeasonStatistics {Id} for AthleteSeason {AthleteSeasonId} with {CategoryCount} categories and {StatCount} total stats",
                entity.Id,
                athleteSeasonId,
                entity.Categories.Count,
                entity.Categories.Sum(c => c.Stats.Count));
        }
    }
}

