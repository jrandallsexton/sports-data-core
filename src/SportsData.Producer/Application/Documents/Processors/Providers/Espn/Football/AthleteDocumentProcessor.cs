using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Eventing.Events.Franchise;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

public class AthleteDocumentProcessor : IProcessDocuments
{
    private readonly ILogger<AthleteDocumentProcessor> _logger;
    private readonly AppDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public AthleteDocumentProcessor(
        ILogger<AthleteDocumentProcessor> logger,
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
        var externalProviderDto = command.Document.FromJson<EspnAthleteDto>();

        // Determine if this entity exists. Do NOT trust that it says it is a new document!
        var exists = await _dataContext.Athletes.AnyAsync(x =>
            x.ExternalIds.Any(z => z.Value == externalProviderDto.Id.ToString() &&
                                   z.Provider == SourceDataProvider.Espn));

        if (exists)
        {
            _logger.LogWarning($"Athlete already exists for {SourceDataProvider.Espn}.");
            return;
        }

        // 1. map to the entity add it
        var newEntityId = Guid.NewGuid();

        // TODO: Get the current franchise id from the athleteDto?
        var newEntity = externalProviderDto.AsEntity(newEntityId, null, command.CorrelationId);
        await _dataContext.AddAsync(newEntity);

        // 2. any headshot (image) for the athlete?
        if (externalProviderDto.Headshot is not null)
        {
            var imgEvt = new ProcessImageRequest(
                externalProviderDto.Headshot.Href.AbsoluteUri,
                Guid.NewGuid(),
                newEntityId,
                $"{newEntityId}.png",
                command.Sport,
                command.Season,
                command.DocumentType,
                command.SourceDataProvider,
                0,
                0,
                null,
                command.CorrelationId,
                CausationId.Producer.AthleteDocumentProcessor);
            await _publishEndpoint.Publish(imgEvt);
        }

        // 3. Raise the integration event
        await _publishEndpoint.Publish(
            new AthleteCreated(
                newEntity.ToCanonicalModel(),
                command.CorrelationId,
                CausationId.Producer.AthleteDocumentProcessor));

        await _dataContext.SaveChangesAsync();

    }
}