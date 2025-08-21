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
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionDrive)]
    public class EventCompetitionDriveDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : FootballDataContext
    {
        private readonly ILogger<EventCompetitionDriveDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IEventBus _publishEndpoint;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

        public EventCompetitionDriveDocumentProcessor(
            ILogger<EventCompetitionDriveDocumentProcessor<TDataContext>> logger,
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
                _logger.LogInformation("Began with {@command}", command);

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
            var externalDto = command.Document.FromJson<EspnEventCompetitionDriveDto>();

            if (externalDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnEventCompetitionDriveDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
            {
                _logger.LogError("EspnEventCompetitionDriveDto Ref is null. {@Command}", command);
                return;
            }

            var competitionId = await GetCompetitionId(command);

            var startFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                externalDto.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
                _logger);

            var endFranchiseSeasonId = await _dataContext.TryResolveFromDtoRefAsync(
                externalDto.EndTeam,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons.Include(x => x.ExternalIds).AsNoTracking(),
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
                    startFranchiseSeasonId,
                    endFranchiseSeasonId);
            }
            else
            {
                await ProcessUpdate(command, externalDto, entity);
            }
        }

        private async Task<Guid> GetCompetitionId(ProcessDocumentCommand command)
        {
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

            return competitionId;
        }
        
        private async Task ProcessNewEntity(
            ProcessDocumentCommand command,
            EspnEventCompetitionDriveDto externalDto,
            Guid competitionId,
            Guid? startFranchiseSeasonId,
            Guid? endFranchiseSeasonId)
        {
            var entity = externalDto.AsEntity(
                command.CorrelationId,
                _externalRefIdentityGenerator,
                competitionId,
                startFranchiseSeasonId,
                endFranchiseSeasonId);

            // TODO: If sequence is "-1", log a warning with the URL

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
