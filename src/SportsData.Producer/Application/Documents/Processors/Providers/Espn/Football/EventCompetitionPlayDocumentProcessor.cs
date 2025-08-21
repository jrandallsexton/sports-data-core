using MassTransit;

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
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionPlay)]
    public class EventCompetitionPlayDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : FootballDataContext
    {
        private readonly ILogger<EventCompetitionPlayDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionPlayDocumentProcessor(
            ILogger<EventCompetitionPlayDocumentProcessor<TDataContext>> logger,
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
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                    throw;
                }
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var externalDto = command.Document.FromJson<EspnEventCompetitionPlayDto>();

            if (externalDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventCompetitionPlayDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
            {
                _logger.LogError("EspnEventCompetitionPlayDto Ref is null. {@Command}", command);
                return;
            }

            if (!command.Season.HasValue)
            {
                _logger.LogError("Command must have a SeasonYear defined");
                throw new InvalidOperationException("SeasonYear must be defined in the command.");
            }

            if (string.IsNullOrEmpty(command.ParentId))
            {
                _logger.LogError("Command must have a ParentId defined for the CompetitionId");
                throw new InvalidOperationException("ParentId must be defined in the command.");
            }

            if (!Guid.TryParse(command.ParentId, out var competitionId))
            {
                _logger.LogError("CompetitionId could not be parsed");
                throw new InvalidOperationException("ParentId must be a valid Guid.");
            }

            var competitionExists = await _dataContext.Competitions
                .AsNoTracking()
                .AnyAsync(x => x.Id == competitionId);

            if (!competitionExists)
            {
                _logger.LogError("Competition not found for {CompetitionId}", competitionId);
                throw new InvalidOperationException($"Competition with ID {competitionId} does not exist.");
            }

            var franchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                externalDto.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            if (franchiseSeasonId is null)
            {
                _logger.LogError("FranchiseSeason could not be resolved from DTO reference: {@DtoRef}", externalDto.Team?.Ref);
                throw new InvalidOperationException("FranchiseSeason could not be resolved from DTO reference.");
            }

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var entity = await _dataContext.Plays
                .Include(x => x.ExternalIds)
                .FirstOrDefaultAsync(x =>
                    x.ExternalIds.Any(z => z.SourceUrlHash == command.UrlHash &&
                                           z.Provider == command.SourceDataProvider));

            if (entity is null)
            {
                await ProcessNewEntity(
                    command,
                    externalDto,
                    competitionId,
                    franchiseSeasonId.Value);
            }
            else
            {
                await ProcessUpdate(command, externalDto, entity);
            }
        }

        private async Task ProcessNewEntity(
            ProcessDocumentCommand command,
            EspnEventCompetitionPlayDto externalDto,
            Guid competitionId,
            Guid franchiseSeasonId)
        {
            var startTeamFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                externalDto.Start.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            var play = externalDto.AsEntity(
                _externalRefIdentityGenerator,
                command.CorrelationId,
                competitionId,
                null,
                franchiseSeasonId,
                startTeamFranchiseSeasonId);

            await _dataContext.Plays.AddAsync(play);
            await _dataContext.SaveChangesAsync();
        }

        private async Task ProcessUpdate(
            ProcessDocumentCommand command,
            EspnEventCompetitionPlayDto externalDto,
            Play entity)
        {
            // TODO: Implement update logic if necessary
            await Task.Delay(100);
        }
    }
}
