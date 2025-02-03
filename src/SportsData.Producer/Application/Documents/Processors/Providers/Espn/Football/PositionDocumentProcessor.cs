using MassTransit;

using Microsoft.EntityFrameworkCore;

using Newtonsoft.Json;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Positions;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    public class PositionDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<PositionDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public PositionDocumentProcessor(
            ILogger<PositionDocumentProcessor> logger,
            AppDataContext dataContext,
            IPublishEndpoint publishEndpoint)
        {
            _logger = logger;
            _dataContext = dataContext;
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

                await ProcessInternal(command);
            }
        }

        private async Task ProcessInternal(ProcessDocumentCommand command)
        {
            // deserialize the DTO
            var espnDto = command.Document.FromJson<EspnAthletePositionDto>(new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore
            });

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var exists = await _dataContext.Positions.AnyAsync(x =>
                x.ExternalIds.Any(z => z.Value == espnDto.Id.ToString() && z.Provider == command.SourceDataProvider));

            if (exists)
            {
                // TODO: Determine what to do here.  Publish the correct event? Pass it directly to the correct handler?
                _logger.LogWarning("Position already exists.");
                return;
            }

            // 1. map to the entity and save it
            var newEntity = espnDto.AsEntity(Guid.NewGuid(), command.CorrelationId);
            _dataContext.Add(newEntity);

            // 2. raise an integration event with the canonical model
            var evt = new PositionCreated(newEntity.ToCanonicalModel(), command.CorrelationId, CausationId.Producer.PositionDocumentProcessor);

            await _publishEndpoint.Publish(evt);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Position, evt);
        }
    }
}
