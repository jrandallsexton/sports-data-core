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
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Season)]
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Seasons)]
    public class SeasonDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<SeasonDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IPublishEndpoint _publishEndpoint;

        public SeasonDocumentProcessor(
            ILogger<SeasonDocumentProcessor<TDataContext>> logger,
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
            // Step 1: Deserialize
            var externalProviderDto = command.Document.FromJson<EspnFootballSeasonDto>();

            if (externalProviderDto is null)
            {
                _logger.LogError("Failed to deserialize document to EspnFootballSeasonDto. {@Command}", command);
                return;
            }

            if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
            {
                _logger.LogError("EspnFootballSeasonDto Ref is null or empty. {@Command}", command);
                return;
            }

            // Step 2: Map DTO -> Canonical Entity
            var mappedSeason = externalProviderDto.AsEntity(_externalRefIdentityGenerator, command.CorrelationId);

            _logger.LogInformation("Mapped season: {@mappedSeason}", mappedSeason);

            // Step 3: Load existing from DB
            var existingSeason = await _dataContext.Seasons
                .Include(s => s.Phases)
                .Include(s => s.ExternalIds)
                .FirstOrDefaultAsync(s => s.Id == mappedSeason.Id);

            if (existingSeason != null)
            {
                await ProcessUpdateAsync(existingSeason, mappedSeason);
            }
            else
            {
                await ProcessNewEntity(command, externalProviderDto);
            }

            // Step 4: Save changes
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Finished processing season {SeasonId}", mappedSeason.Id);
        }

        private async Task ProcessNewEntity(ProcessDocumentCommand command, EspnFootballSeasonDto dto)
        {
            var newEntity = dto.AsEntity(_externalRefIdentityGenerator, command.CorrelationId);

            if (dto.Types?.Ref is not null)
            {
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: Guid.NewGuid().ToString(),
                    ParentId: newEntity.Id.ToString(),
                    Uri: dto.Types.Ref,
                    Sport: command.Sport,
                    SeasonYear: dto.Year,
                    DocumentType: DocumentType.SeasonType,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.SeasonDocumentProcessor
                ));
            }

            // TODO: Rankings?

            // Had to remove this for now as it creates a circular dependency between SeasonDocumentProcessor and AthleteSeasonDocumentProcessor
            //if (dto.Athletes?.Ref is not null)
            //{
            //    await _publishEndpoint.Publish(new DocumentRequested(
            //        Id: Guid.NewGuid().ToString(),
            //        ParentId: null,  // we do not have it; AthleteSeasonDocumentProcessor will need to find the parent Athlete
            //        Uri: dto.Athletes.Ref,
            //        Sport: command.Sport,
            //        SeasonYear: dto.Year,
            //        DocumentType: DocumentType.AthleteSeason,
            //        SourceDataProvider: SourceDataProvider.Espn,
            //        CorrelationId: command.CorrelationId,
            //        CausationId: CausationId.Producer.SeasonDocumentProcessor
            //    ));
            //}

            if (dto.Futures?.Ref is not null)
            {
                await _publishEndpoint.Publish(new DocumentRequested(
                    Id: Guid.NewGuid().ToString(),
                    ParentId: newEntity.Id.ToString(),
                    Uri: dto.Futures.Ref,
                    Sport: command.Sport,
                    SeasonYear: dto.Year,
                    DocumentType: DocumentType.SeasonFuture,
                    SourceDataProvider: SourceDataProvider.Espn,
                    CorrelationId: command.CorrelationId,
                    CausationId: CausationId.Producer.SeasonDocumentProcessor
                    ));
            }

            await _dataContext.Seasons.AddAsync(newEntity);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Created new Season entity: {SeasonId}", newEntity.Id);
        }

        private async Task ProcessUpdateAsync(Season existingSeason, Season mappedSeason)
        {
            _logger.LogWarning("Season update detected. Not implemented");
            await Task.Delay(100);
            //// Update scalar properties
            //existingSeason.Year = mappedSeason.Year;
            //existingSeason.Name = mappedSeason.Name;
            //existingSeason.StartDate = mappedSeason.StartDate;
            //existingSeason.EndDate = mappedSeason.EndDate;
            //existingSeason.ActivePhaseId = mappedSeason.ActivePhaseId;

            //// Replace Phases wholesale
            //_dataContext.SeasonPhases.RemoveRange(existingSeason.Phases);
            //existingSeason.Phases.Clear();
            //foreach (var phase in mappedSeason.Phases)
            //{
            //    existingSeason.Phases.Add(phase);
            //}

            //// Replace ExternalIds wholesale
            //_dataContext.SeasonExternalIds.RemoveRange(existingSeason.ExternalIds);
            //existingSeason.ExternalIds.Clear();
            //foreach (var extId in mappedSeason.ExternalIds)
            //{
            //    existingSeason.ExternalIds.Add(extId);
            //}

            //_logger.LogInformation("Updated existing Season with Id {SeasonId}", existingSeason.Id);

            //return Task.CompletedTask;
        }
    }
}
