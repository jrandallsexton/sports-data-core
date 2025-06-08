using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Athlete)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthleteBySeason)]
public class AthleteDocumentProcessor : IProcessDocuments
{
    private readonly ILogger<AthleteDocumentProcessor> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public AthleteDocumentProcessor(
        ILogger<AthleteDocumentProcessor> logger,
        FootballDataContext dataContext,
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
        var externalProviderDto = command.Document.FromJson<EspnFootballAthleteDto>();

        // Determine if this entity exists. Do NOT trust that it says it is a new document!
        var exists = await _dataContext.Athletes
            .Include(x => x.ExternalIds)
            .AsNoTracking()
            .AnyAsync(x => x.ExternalIds.Any(z => z.Value == externalProviderDto.Id.ToString() &&
                                                  z.Provider == command.SourceDataProvider));

        if (exists)
        {
            // TODO: Eventually we need to handle updates to existing entities.
            _logger.LogWarning($"Athlete already exists for {command.SourceDataProvider}.");
            return;
        }

        // 1. map to the entity add it
        var newEntityId = Guid.NewGuid();

        // TODO: Get the current franchise Id from the athleteDto?
        var newEntity = externalProviderDto.AsEntity(newEntityId, null, command.CorrelationId);
        await _dataContext.AddAsync(newEntity);

        // 2. any headshot (image) for the AthleteDto?
        if (externalProviderDto.Headshot is not null)
        {
            var newImgId = Guid.NewGuid();
            var imgEvt = new ProcessImageRequest(
                externalProviderDto.Headshot.Href.AbsoluteUri,
                newImgId,
                newEntityId,
                $"{newEntityId}-{newImgId}.png",
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