using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Football.Ncaa
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
            var franchiseEntity = externalProviderDto.AsFranchiseEntity(newFranchiseId, command.CorrelationId);
            await _dataContext.AddAsync(franchiseEntity);

            // 2. any logos on the dto?
            var events = new List<ProcessImageRequest>();
            externalProviderDto.Logos.ForEach(logo =>
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
                    null));
            });

            if (events.Count > 0)
                await _publishEndpoint.PublishBatch(events);

            // 3. raise an event
            var evt = new FranchiseCreated(franchiseEntity.ToCanonicalModel());
            await _publishEndpoint.Publish(evt);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Franchise, evt);
        }
    }
}
