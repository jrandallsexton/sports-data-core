using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    [DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.SeasonType)]
    public class SeasonTypeDocumentProcessor<TDataContext> : IProcessDocuments
        where TDataContext : BaseDataContext
    {
        private readonly ILogger<SeasonTypeDocumentProcessor<TDataContext>> _logger;
        private readonly TDataContext _dataContext;
        private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;
        private readonly IPublishEndpoint _publishEndpoint;

        public SeasonTypeDocumentProcessor(
            ILogger<SeasonTypeDocumentProcessor<TDataContext>> logger,
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
            // deserialize the DTO
            var externalProviderDto = command.Document.FromJson<EspnFootballSeasonTypeDto>();

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

            if (!Guid.TryParse(command.ParentId, out var seasonId))
            {
                _logger.LogError("Invalid ParentId: {ParentId}", command.ParentId);
                return;
            }

            var existingSeasonType = await _dataContext.Seasons
                .Include(x => x.Phases)
                .Where(x => x.Id == seasonId)
                .FirstOrDefaultAsync();

            if (existingSeasonType == null)
            {
                _logger.LogError("Parent Season could not be found");
                throw new Exception("Parent Season could not be found");
            }

            var seasonPhase = externalProviderDto.AsEntity(seasonId, _externalRefIdentityGenerator, command.CorrelationId);

            // does the phase already exist on the season?
            var existingPhase = existingSeasonType.Phases.FirstOrDefault(x => x.Id == seasonPhase.Id);

            if (existingPhase != null)
            {
                // update
                existingPhase.Name = seasonPhase.Name;
                existingPhase.Abbreviation = seasonPhase.Abbreviation;
                existingPhase.StartDate = seasonPhase.StartDate;
                existingPhase.EndDate = seasonPhase.EndDate;
                _dataContext.Update(existingPhase);
            }
            else
            {
                // new
                existingSeasonType.Phases.Add(seasonPhase);

                // Source Groups
                if (externalProviderDto.Groups?.Ref is not null)
                {
                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: Guid.NewGuid().ToString(),
                        ParentId: seasonPhase.Id.ToString(),
                        Uri: externalProviderDto.Groups.Ref,
                        Sport: Sport.FootballNcaa,
                        SeasonYear: externalProviderDto.Year,
                        DocumentType: DocumentType.GroupSeason,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.SeasonTypeDocumentProcessor
                    ));
                }

                // Source Weeks
                if (externalProviderDto.Weeks?.Ref is not null)
                {
                    await _publishEndpoint.Publish(new DocumentRequested(
                        Id: Guid.NewGuid().ToString(),
                        ParentId: seasonPhase.Id.ToString(),
                        Uri: externalProviderDto.Weeks.Ref,
                        Sport: Sport.FootballNcaa,
                        SeasonYear: externalProviderDto.Year,
                        DocumentType: DocumentType.SeasonTypeWeek,
                        SourceDataProvider: SourceDataProvider.Espn,
                        CorrelationId: command.CorrelationId,
                        CausationId: CausationId.Producer.SeasonTypeDocumentProcessor
                    ));
                }
            }

            await _dataContext.SaveChangesAsync();
        }
    }
}
