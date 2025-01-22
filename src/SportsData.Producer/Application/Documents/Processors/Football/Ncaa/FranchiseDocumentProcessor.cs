﻿using MassTransit;

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
            var espnFranchiseDto = command.Document.FromJson<EspnFranchiseDto>();

            // Determine if this entity exists. Do NOT trust that it says it is a new document!
            var exists = await _dataContext.Franchises.AnyAsync(x =>
                x.ExternalIds.Any(z => z.Value == espnFranchiseDto.Id.ToString() && z.Provider == SourceDataProvider.Espn));

            if (exists)
            {
                _logger.LogWarning($"Franchise already exists for {SourceDataProvider.Espn}.");
                return;
            }

            // 1. map to the entity and save it
            var franchiseEntity = espnFranchiseDto.AsFranchiseEntity(Guid.NewGuid(), command.CorrelationId);
            await _dataContext.AddAsync(franchiseEntity);
            await _dataContext.SaveChangesAsync();

            // 2. raise an event
            var evt = new FranchiseCreated(franchiseEntity.ToCanonicalModel());
            await _bus.Publish(evt);

            _logger.LogInformation("New {@type} event {@evt}", DocumentType.Franchise, evt);
        }
    }
}
