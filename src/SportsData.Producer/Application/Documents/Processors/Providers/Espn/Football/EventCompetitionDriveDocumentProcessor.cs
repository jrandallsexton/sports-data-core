using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionDrive)]
    public class EventCompetitionDriveDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : FootballDataContext
    {
        private readonly ILogger<EventCompetitionDriveDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionDriveDocumentProcessor(
            ILogger<EventCompetitionDriveDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IPublishEndpoint publishEndpoint,
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
                _logger.LogInformation("Began with {@command}", command);

                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            var externalDto = command.Document.FromJson<EspnEventCompetitionDriveDto>();

            if (externalDto is null)
            {
                _logger.LogError($"Error deserializing {command.DocumentType}");
                throw new InvalidOperationException($"Deserialization returned null for {command.DocumentType}");
            }

            if (!Guid.TryParse(command.ParentId, out var competitionId))
            {
                _logger.LogError("CompetitionId could not be parsed");
                throw new InvalidOperationException("ParentId must be a valid Guid.");
            }

            var startFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                externalDto.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds),
                _logger);

            if (startFranchiseSeasonId is null)
            {
                _logger.LogError("FranchiseSeason could not be resolved from DTO reference: {@DtoRef}", externalDto.Team?.Ref);
                throw new InvalidOperationException("FranchiseSeason could not be resolved from DTO reference.");
            }

            var endFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                externalDto.EndTeam,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds),
                _logger);

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var entity = await _dataContext.Drives
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
                    startFranchiseSeasonId.Value,
                    endFranchiseSeasonId);
            }
            else
            {
                await ProcessUpdate(command, externalDto, entity);
            }
        }

        private async Task ProcessNewEntity(
            ProcessDocumentCommand command,
            EspnEventCompetitionDriveDto externalDto,
            Guid competitionId,
            Guid startFranchiseSeasonId,
            Guid? endFranchiseSeasonId)
        {
            var entity = externalDto.AsEntity(
                _externalRefIdentityGenerator,
                competitionId,
                startFranchiseSeasonId,
                endFranchiseSeasonId);

            await _dataContext.Drives.AddAsync(entity);
            await _dataContext.SaveChangesAsync();
        }

        private async Task ProcessUpdate(
            ProcessDocumentCommand command,
            object externalDto,
            Drive entity)
        {
            // TODO: Implement update logic if necessary
            await Task.Delay(100);
        }
    }
}
