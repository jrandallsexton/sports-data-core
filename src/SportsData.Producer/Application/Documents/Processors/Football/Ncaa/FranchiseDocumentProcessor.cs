using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Franchise;
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
        private readonly IBus _bus;

        public FranchiseDocumentProcessor(
            ILogger<FranchiseDocumentProcessor> logger,
            AppDataContext dataContext,
            IBus bus)
        {
            _logger = logger;
            _dataContext = dataContext;
            _bus = bus;
        }

        public async Task ProcessAsync(ProcessDocumentCommand command)
        {
            var espnFranchise = command.Document.FromJson<EspnFranchiseDto>();

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var exists = await _dataContext.Franchises.AnyAsync(x =>
                x.ExternalIds.Any(z => z.Value == espnFranchise.Id.ToString() && z.Provider == SourceDataProvider.Espn));

            if (exists)
            {
                _logger.LogWarning($"Franchise already exists for {SourceDataProvider.Espn}.");
                return;
            }

            // 1. map to the entity and save it
            // TODO: Move to extension method?
            var franchiseId = Guid.NewGuid();
            var franchiseEntity = new Franchise()
            {
                Id = franchiseId,
                Abbreviation = espnFranchise.Abbreviation,
                ColorCodeHex = espnFranchise.Color,
                DisplayName = espnFranchise.DisplayName,
                CreatedUtc = DateTime.UtcNow,
                CreatedBy = command.CorrelationId,
                ExternalIds = [new FranchiseExternalId() { Value = espnFranchise.Id.ToString(), Provider = SourceDataProvider.Espn }],
                GlobalId = Guid.NewGuid(),
                DisplayNameShort = espnFranchise.ShortDisplayName,
                IsActive = espnFranchise.IsActive,
                Name = espnFranchise.Name,
                Nickname = espnFranchise.Nickname,
                Slug = espnFranchise.Slug,
                Logos = espnFranchise.Logos.Select(x => new FranchiseLogo()
                {
                    Id = Guid.NewGuid(),
                    CreatedBy = command.CorrelationId,
                    CreatedUtc = DateTime.UtcNow,
                    FranchiseId = franchiseId,
                    Height = x.Height,
                    Width = x.Width,
                    Url = x.Href.ToString()
                }).ToList()
            };
            await _dataContext.AddAsync(franchiseEntity);
            await _dataContext.SaveChangesAsync();

            // 2. raise an event
            // TODO: Determine if I want to publish all data in the event instead of this chatty stuff
            var evt = new FranchiseCreated()
            {
                Id = espnFranchise.Id.ToString(),
                Name = nameof(FranchiseCreated)
            };
            await _bus.Publish(evt);
            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Franchise, evt);
        }
    }
}
