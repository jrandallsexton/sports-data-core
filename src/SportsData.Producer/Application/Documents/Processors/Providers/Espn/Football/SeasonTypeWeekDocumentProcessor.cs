using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonTypeWeek)]
    public class SeasonTypeWeekDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<SeasonTypeWeekDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IPublishEndpoint _publishEndpoint;

        public SeasonTypeWeekDocumentProcessor(
            ILogger<SeasonTypeWeekDocumentProcessor<TDataContext>> logger,
            TDataContext dataContext,
            IGenerateExternalRefIdentities externalRefIdentityGenerator,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dataContext = dataContext;
            _externalRefIdentityGenerator = externalRefIdentityGenerator;
            _publishEndpoint = publishEndpoint;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            using (_logger.BeginScope(new Dictionary<string, object> {
                       ["CorrelationId"] = command.CorrelationId,
                       ["OriginalUri"] = command.OriginalUri is null ? string.Empty : command.OriginalUri.ToString()
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
            if (command.Season is null) 
            {
                _logger.LogError("Command does not contain a valid Season. {@Command}", command);
                return;
            }

            if (!Guid.TryParse(command.ParentId, out var seasonPhaseId))
            {
                _logger.LogError("SeasonPhaseId could not be parsed");
                return;
            }

            var seasonPhase = await _dataContext.SeasonPhases
                .Include(x => x.Weeks)
                .ThenInclude(w => w.ExternalIds)
                .Where(x => x.Id == seasonPhaseId)
                .FirstOrDefaultAsync();

            if (seasonPhase == null)
            {
                _logger.LogError("Could not find SeasonPhase with Id {SeasonPhaseId}", seasonPhaseId);
                return;
            }
            
            var externalProviderDto = command.Document.FromJson<EspnFootballSeasonTypeWeekDto>();

            if (externalProviderDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnFootballSeasonTypeWeekDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
            {
                _logger.LogError("EspnFootballSeasonTypeWeekDto Ref is null or empty. {@Command}", command);
                return;
            }

            var dtoIdentity = _externalRefIdentityGenerator.Generate(externalProviderDto.Ref);

            var seasonWeek = seasonPhase.Weeks
                .FirstOrDefault(w => w.ExternalIds.Any(id => id.SourceUrlHash == dtoIdentity.UrlHash &&
                                                             id.Provider == command.SourceDataProvider));
            if (seasonWeek is null)
            {
                await ProcessNewEntity(externalProviderDto, seasonPhase, command, dtoIdentity);
            }
            else
            {
                await ProcessExistingEntity();
            }

        }

        private async Task ProcessNewEntity(
            EspnFootballSeasonTypeWeekDto dto,
            SeasonPhase seasonPhase,
            ProcessDocumentCommand command,
            ExternalRefIdentity dtoIdentity)
        {
            var seasonWeek = dto.AsEntity(
                seasonPhase.SeasonId,
                seasonPhase.Id,
                _externalRefIdentityGenerator,
                command.CorrelationId);

            if (dto.Rankings?.Ref is not null)
            {
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: dtoIdentity.UrlHash,
                    ParentId: seasonWeek.Id.ToString(),
                    Uri: dto.Rankings.Ref,
                    Sport: command.Sport,
                    SeasonYear: command.Season!.Value,
                    DocumentType: DocumentType.SeasonTypeWeekRankings,
                    SourceDataProvider: command.SourceDataProvider,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.SeasonTypeWeekDocumentProcessor
                ));
            }

            await _dataContext.SeasonWeeks.AddAsync(seasonWeek);
            await _dataContext.SaveChangesAsync();
        }

        private async Task ProcessExistingEntity()
        {
            _logger.LogError("Update detected. Not implemented");
            await Task.Delay(100);
        }
    }
}
