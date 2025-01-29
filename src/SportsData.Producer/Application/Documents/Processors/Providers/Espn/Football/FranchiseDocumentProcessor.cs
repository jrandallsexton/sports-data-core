using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football
{
    public class FranchiseDocumentProcessor : IProcessDocuments
    {
        private readonly ILogger<FranchiseDocumentProcessor> _logger;
        private readonly AppDataContext _dataContext;
        private readonly IPublishEndpoint _publishEndpoint;

        public FranchiseDocumentProcessor(
            ILogger<FranchiseDocumentProcessor> logger,
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
            var externalProviderDto = command.Document.FromJson<EspnFranchiseDto>();

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var exists = await _dataContext.Franchises.AnyAsync(x =>
                x.ExternalIds.Any(z => z.Value == externalProviderDto.Id.ToString() && z.Provider == SourceDataProvider.Espn));

            if (exists)
            {
                _logger.LogWarning($"Franchise already exists for {SourceDataProvider.Espn}.");
                return;
            }

            // 1. map to the entity add it
            var newFranchiseId = Guid.NewGuid();
            var franchiseEntity = externalProviderDto.AsEntity(newFranchiseId, command.CorrelationId);
            await _dataContext.AddAsync(franchiseEntity);

            // TODO: find the venue associated with this franchise

            // 2. any logos on the dto?
            var events = new List<ProcessImageRequest>();
            externalProviderDto.Logos?.ForEach(logo =>
            {
                var imgId = Guid.NewGuid();
                events.Add(new ProcessImageRequest(
                    logo.Href.AbsoluteUri,
                    imgId,
                    newFranchiseId,
                    $"{newFranchiseId}.png",
                    command.Sport,
                    command.Season,
                    command.DocumentType,
                    command.SourceDataProvider,
                    0,
                    0,
                    null,
                    command.CorrelationId,
                    CausationId.Producer.FranchiseDocumentProcessor));
            });

            if (events.Count > 0)
            {
                _logger.LogInformation($"Requesting {events.Count} images for {command.DocumentType} {command.Season}");
                await _publishEndpoint.PublishBatch(events);
            }

            // 3. raise an event
            await _publishEndpoint.Publish(
                new FranchiseCreated(
                    franchiseEntity.ToCanonicalModel(),
                    command.CorrelationId,
                    CausationId.Producer.FranchiseDocumentProcessor));

            await _dataContext.SaveChangesAsync();
        }
    }
}
