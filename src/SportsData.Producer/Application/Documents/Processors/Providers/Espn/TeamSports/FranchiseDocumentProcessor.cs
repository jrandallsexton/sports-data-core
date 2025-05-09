using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.TeamSports;

public class FranchiseDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<FranchiseDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public FranchiseDocumentProcessor(
        ILogger<FranchiseDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
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
        var entity = await _dataContext.Franchises.FirstOrDefaultAsync(x =>
            x.ExternalIds.Any(z => z.Value == externalProviderDto.Id.ToString() &&
                                   z.Provider == command.SourceDataProvider));

        if (entity is null)
        {
            await ProcessNewEntity(command, externalProviderDto);
        }
        else
        {
            await ProcessUpdate(command, externalProviderDto, entity);
        }

    }

    private async Task ProcessNewEntity(ProcessDocumentCommand command, EspnFranchiseDto dto)
    {
        // 1. map to the entity add it
        var newFranchiseId = Guid.NewGuid();
        var newEntity = dto.AsEntity(newFranchiseId, command.CorrelationId);
        await _dataContext.AddAsync(newEntity);

        if (dto.Venue is not null && dto.Venue.Id > 0)
        {
            var venueEntity = await _dataContext.Venues
                .Include(x => x.ExternalIds)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ExternalIds.Any(z =>
                    z.Provider == command.SourceDataProvider &&
                    z.Value == dto.Venue.Id.ToString()));

            if (venueEntity != null)
            {
                newEntity.VenueId = venueEntity.Id;
            }
            else
            {
                // TODO: What to do if the venue does not exist?
                // We have it on the Espn dto, but not in our db.
            }
        }

        // 2. any logos on the dto?
        var events = new List<ProcessImageRequest>();
        dto.Logos?.ForEach(logo =>
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

        // 3. Raise the integration event
        await _publishEndpoint.Publish(
            new FranchiseCreated(
                newEntity.ToCanonicalModel(),
                command.CorrelationId,
                CausationId.Producer.FranchiseDocumentProcessor));

        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessUpdate(ProcessDocumentCommand command, EspnFranchiseDto dto, Franchise entity)
    {
        if (dto.Venue is not null && dto.Venue.Id > 0)
        {
            var venueEntity = await _dataContext.Venues
                .Include(x => x.ExternalIds)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ExternalIds.Any(z =>
                    z.Provider == command.SourceDataProvider &&
                    z.Value == dto.Venue.Id.ToString()));

            if (venueEntity != null)
            {
                entity.VenueId = venueEntity.Id;
            }
            else
            {
                // TODO: What to do if the venue does not exist?
                // We have it on the Espn dto, but not in our db.
            }
        }

        await _dataContext.SaveChangesAsync();
    }
}