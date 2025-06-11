using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Position)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthletePosition)]
public class AthletePositionDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<AthletePositionDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;

    public AthletePositionDocumentProcessor(
        ILogger<AthletePositionDocumentProcessor<TDataContext>> logger,
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
        var externalProviderDto = command.Document.FromJson<EspnAthletePositionDto>();

        if (externalProviderDto is null)
        {
            _logger.LogError($"Error deserializing {command.DocumentType}");
            throw new InvalidOperationException($"Deserialization returned null for EspnVenueDto. CorrelationId: {command.CorrelationId}");
        }

        // TODO: Validate DTO
        var exists = false;
        //var exists = await _dataContext.AthletePositions
        //    .Include(x => x.ExternalIds)
        //    .Where(x => x.ExternalIds.Any(y => y.Value == dto.Id))
        //    .FirstOrDefaultAsync();

        if (exists)
        {
            await ProcessUpdate(command, externalProviderDto);
        }
        else
        {
            await ProcessNewEntity(command, externalProviderDto);
        }
    }

    private async Task ProcessNewEntity(ProcessDocumentCommand command, EspnAthletePositionDto dto)
    {
        var newPositionId = Guid.NewGuid();

        var parentId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Parent,
            command.SourceDataProvider,
            () => _dataContext.AthletePositions,
            _logger);

        var entity = dto.AsEntity(newPositionId, parentId);

        _dataContext.AthletePositions.Add(entity);

        var evt = new AthletePositionCreated(
            entity.AsCanonical(),
            command.CorrelationId,
            CausationId.Producer.AthletePositionDocumentProcessor);

        await _publishEndpoint.Publish(evt);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("New AthletePosition {@evt}", evt);
    }

    private async Task ProcessUpdate(ProcessDocumentCommand command, EspnAthletePositionDto dto)
    {
        var entity = await _dataContext.AthletePositions.Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.ExternalIds.Any(y => y.Value == dto.Id.ToString()));

        if (entity is null)
        {
            _logger.LogWarning("AthletePosition entity not found for DTO ID {DtoId} during update.", dto.Id);
            throw new InvalidOperationException($"No AthletePosition found for external ID {dto.Id}");
        }

        var updated = false;

        if (entity.Name != dto.Name)
        {
            _logger.LogInformation("Updating Name from {Old} to {New}", entity.Name, dto.Name);
            entity.Name = dto.Name;
            updated = true;
        }

        if (entity.DisplayName != dto.DisplayName)
        {
            _logger.LogInformation("Updating DisplayName from {Old} to {New}", entity.DisplayName, dto.DisplayName);
            entity.DisplayName = dto.DisplayName;
            updated = true;
        }

        if (entity.Abbreviation != dto.Abbreviation)
        {
            _logger.LogInformation("Updating Abbreviation from {Old} to {New}", entity.Abbreviation, dto.Abbreviation);
            entity.Abbreviation = dto.Abbreviation;
            updated = true;
        }

        if (entity.Leaf != dto.Leaf)
        {
            _logger.LogInformation("Updating Leaf from {Old} to {New}", entity.Leaf, dto.Leaf);
            entity.Leaf = dto.Leaf;
            updated = true;
        }

        var newParentId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Parent,
            command.SourceDataProvider,
            () => _dataContext.AthletePositions,
            _logger);

        if (entity.ParentId != newParentId)
        {
            _logger.LogInformation("Updating ParentId from {Old} to {New}", entity.ParentId, newParentId);
            entity.ParentId = newParentId;
            updated = true;
        }

        if (updated)
        {
            await _dataContext.SaveChangesAsync();

            var evt = new AthletePositionUpdated(
                entity.AsCanonical(),
                command.CorrelationId,
                CausationId.Producer.AthletePositionDocumentProcessor);

            await _publishEndpoint.Publish(evt);
            _logger.LogInformation("Updated AthletePosition {@evt}", evt);
        }
        else
        {
            _logger.LogInformation("No changes detected for AthletePosition {Id}", entity.Id);
        }
    }
}
