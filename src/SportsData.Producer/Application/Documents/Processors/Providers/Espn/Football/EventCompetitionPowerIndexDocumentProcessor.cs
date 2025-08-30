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

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionPowerIndex)]
    public class EventCompetitionPowerIndexDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : TeamSportDataContext
    {
        private readonly ILogger<EventCompetitionPowerIndexDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _identityGenerator;

        public EventCompetitionPowerIndexDocumentProcessor(
            ILogger<EventCompetitionPowerIndexDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IEventBus publishEndpoint,
            IGenerateExternalRefIdentities identityGenerator)
        {
            _logger = logger;
            _dataContext = dataContext;
            _publishEndpoint = publishEndpoint;
            _identityGenerator = identityGenerator;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = command.CorrelationId
            }))
            {
                _logger.LogInformation("Processing PowerIndexDocument with {@Command}", command);
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
            var dto = command.Document.FromJson<EspnEventCompetitionPowerIndexDto>();

            if (dto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventCompetitionPowerIndexDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(dto.Ref?.ToString()))
            {
                _logger.LogError("EspnEventCompetitionPowerIndexDto Ref is null or empty. {@Command}", command);
                return;
            }

            // Resolve Competition
            if (!Guid.TryParse(command.ParentId, out var competitionId))
            {
                _logger.LogError("Invalid or missing Competition ID in ParentId");
                throw new InvalidOperationException("Missing or invalid parent ID");
            }

            var competition = await _dataContext.Competitions
                .Include(x => x.PowerIndexes)
                .FirstOrDefaultAsync(x => x.Id == competitionId);

            if (competition is null)
            {
                // TODO: Request sourcing of the competition document?
                _logger.LogError("Competition not found for ID {CompetitionId}", competitionId);
                throw new InvalidOperationException($"Competition with ID {competitionId} does not exist.");
            }

            // Resolve FranchiseSeasonId from Team ref
            var franchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                dto.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            if (franchiseSeasonId is null)
            {
                var teamHash = HashProvider.GenerateHashFromUri(dto.Team.Ref);

                _logger.LogWarning("FranchiseSeason not found for hash {Hash}, publishing sourcing request.", teamHash);

                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: teamHash,
                    ParentId: string.Empty,
                    Uri: dto.Team.Ref.ToCleanUri(),
                    Sport: command.Sport,
                    SeasonYear: command.Season,
                    DocumentType: DocumentType.TeamSeason,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.EventCompetitionPowerIndexDocumentProcessor
                ));
                await _dataContext.OutboxPings.AddAsync(new OutboxPing());
                await _dataContext.SaveChangesAsync();

                throw new InvalidOperationException("FranchiseSeason not found.");
            }

            foreach (var stat in dto.Stats)
            {
                var powerIndexName = stat.Name.Trim().ToLowerInvariant();

                var powerIndex = await _dataContext.PowerIndexes
                    .FirstOrDefaultAsync(p => p.Name.ToLower() == powerIndexName.ToLower());

                if (powerIndex is null)
                {
                    powerIndex = new PowerIndex()
                    {
                        Id = Guid.NewGuid(),
                        Name = stat.Name,
                        DisplayName = stat.DisplayName,
                        Description = stat.Description,
                        Abbreviation = stat.Abbreviation,
                        CreatedBy = command.CorrelationId
                    };
                    await _dataContext.PowerIndexes.AddAsync(powerIndex);
                }

                var index = stat.AsEntity(
                    _identityGenerator,
                    dto.Ref,
                    powerIndex.Id,
                    competitionId,
                    franchiseSeasonId.Value,
                    command.CorrelationId);

                await _dataContext.CompetitionPowerIndexes.AddAsync(index);
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}
