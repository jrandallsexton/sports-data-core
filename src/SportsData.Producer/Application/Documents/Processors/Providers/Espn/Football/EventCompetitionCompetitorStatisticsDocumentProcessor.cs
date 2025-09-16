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
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorStatistics)]
    public class EventCompetitionCompetitorStatisticsDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionCompetitorStatisticsDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _eventBus;
        private readonly IGenerateExternalRefIdentities _identityGenerator;

        public EventCompetitionCompetitorStatisticsDocumentProcessor(
            ILogger<EventCompetitionCompetitorStatisticsDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus eventBus,
            IGenerateExternalRefIdentities identityGenerator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _eventBus = eventBus;
            _identityGenerator = identityGenerator;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId
            }))
            {
                _logger.LogInformation("Processing EventCompetitionCompetitorStatistics for {@Command}", command);

                try
                {
                    await ProcessInternalAsync(command);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing EventCompetitionCompetitorStatistics. {@Command}", command);
                    throw;
                }
            }
        }

        private async Task ProcessInternalAsync(ProcessDocumentCommand command)
        {
            var dto = command.Document.FromJson<EspnEventCompetitionCompetitorStatisticsDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize EventCompetitionCompetitorStatisticsDto.");
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var competitionId))
            {
                _logger.LogError("Invalid or missing Competition ID in ParentId.");
                throw new InvalidOperationException("Missing or invalid ParentId.");
            }

            var competition = await _dataContext.Competitions
                .FirstOrDefaultAsync(x => x.Id == competitionId);

            if (competition is null)
            {
                _logger.LogError("Competition not found for ID {CompetitionId}", competitionId);
                throw new InvalidOperationException($"Competition {competitionId} not found.");
            }

            // Resolve FranchiseSeason
            var franchiseSeasonIdentity = _identityGenerator.Generate(dto.Team.Ref);
            var franchiseSeason = await _dataContext.FranchiseSeasons
                .Include(x => x.ExternalIds)
                .FirstOrDefaultAsync(x => x.ExternalIds.Any(z => z.Value == franchiseSeasonIdentity.UrlHash));

            if (franchiseSeason is null)
            {
                _logger.LogError("FranchiseSeason not found for URL hash {Hash}", franchiseSeasonIdentity.UrlHash);
                throw new InvalidOperationException($"FranchiseSeason not found for identity {franchiseSeasonIdentity.CanonicalId}");
            }

            // Blow away any existing stats for this team/competition
            var existing = await _dataContext.CompetitionCompetitorStatistics
                .Include(x => x.Categories)
                .ThenInclude(x => x.Stats)
                .FirstOrDefaultAsync(x =>
                    x.CompetitionId == competition.Id &&
                    x.FranchiseSeasonId == franchiseSeason.Id);

            if (existing is not null)
            {
                _dataContext.CompetitionCompetitorStatistics.Remove(existing);
                await _dataContext.SaveChangesAsync();
                _logger.LogInformation("Existing CompetitionCompetitorStatistic removed for FranchiseSeason {FranchiseSeasonId}, Competition {CompetitionId}", franchiseSeason.Id, competition.Id);
            }

            // Create new record
            var entity = dto.AsEntity(
                franchiseSeasonId: franchiseSeason.Id,
                competitionId: competition.Id,
                externalRefIdentityGenerator: _identityGenerator,
                correlationId: command.CorrelationId);

            await _dataContext.CompetitionCompetitorStatistics.AddAsync(entity);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Inserted CompetitionCompetitorStatistic for FranchiseSeason {FranchiseSeasonId}, Competition {CompetitionId}", franchiseSeason.Id, competition.Id);
        }
    }
}
