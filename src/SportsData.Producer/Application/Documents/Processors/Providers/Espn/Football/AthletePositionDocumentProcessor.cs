using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Athletes;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Position)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.AthletePosition)]
public class AthletePositionDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<AthletePositionDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IEventBus _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public AthletePositionDocumentProcessor(
        ILogger<AthletePositionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator)
    {
        _logger = logger;
        _dataContext = dataContext;
        _publishEndpoint = publishEndpoint;
        _externalRefIdentityGenerator = externalRefIdentityGenerator;
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
            _logger.LogError("Failed to deserialize document to EspnAthletePositionDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalProviderDto.Ref?.ToString()))
        {
            _logger.LogError("EspnAthletePositionDto Ref is null. {@Command}", command);
            return;
        }

        var exists = await _dataContext.AthletePositions
            .AnyAsync(x => x.ExternalIds.Any(z => z.Value == command.UrlHash &&
                                                  z.Provider == command.SourceDataProvider));

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
        // 1️ Normalize the incoming Name for canonical matching
        var normalizedName = dto.Name.ToCanonicalForm();

        // 2️ Try to find an existing AthletePosition by canonical Name
        var existing = await _dataContext.AthletePositions
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Name == normalizedName);

        if (existing != null)
        {
            _logger.LogInformation("Found existing AthletePosition with same Name '{Name}'. Attaching new ExternalId.", normalizedName);

            var sourceUrlHash = HashProvider.GenerateHashFromUri(dto.Ref);

            await _dataContext.AthletePositionExternalIds.AddAsync(new AthletePositionExternalId
            {
                Id = Guid.NewGuid(),
                AthletePositionId = existing.Id,
                Provider = command.SourceDataProvider,
                Value = sourceUrlHash,
                SourceUrlHash = sourceUrlHash,
                SourceUrl = dto.Ref.ToCleanUrl()
            });

            existing.ModifiedUtc = DateTime.UtcNow;
            existing.ModifiedBy = command.CorrelationId;
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Added new ExternalId to existing AthletePosition {Id}", existing.Id);
            return;
        }

        // 3️ No match found — proceed to create brand new AthletePosition
        _logger.LogInformation("No existing AthletePosition found for Name '{Name}'. Creating new entity.", normalizedName);
        
        Guid? parentId = null;

        if (dto.Parent is not null)
        {
            parentId = await _dataContext.ResolveIdAsync<
                AthletePosition, AthletePositionExternalId>(
                dto.Parent,
                command.SourceDataProvider,
                () => _dataContext.AthletePositions,
                externalIdsNav: "ExternalIds",
                key: p => p.Id,
                CancellationToken.None);

            if (parentId is null)
            {
                _logger.LogError(
                    "Unable to resolve ParentId for AthletePosition with Name '{Name}' (Ref: {Ref}). Likely parent position has not yet been sourced. Throwing to allow Hangfire retry.",
                    dto.Name,
                    dto.Parent?.Ref);

                throw new InvalidOperationException($"Parent position not yet available for '{dto.Name}'. Will retry.");
            }
        }

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            parentId);

        _dataContext.AthletePositions.Add(entity);

        await _dataContext.SaveChangesAsync();

        var evt = new AthletePositionCreated(
            entity.AsCanonical(),
            command.CorrelationId,
            CausationId.Producer.AthletePositionDocumentProcessor);

        await _publishEndpoint.Publish(evt);

        _logger.LogInformation("Created new AthletePosition {@evt}", evt);
    }


    private async Task ProcessUpdate(ProcessDocumentCommand command, EspnAthletePositionDto dto)
    {
        var entity = await _dataContext.AthletePositions.Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.ExternalIds.Any(z => z.Value == command.UrlHash &&
                                                             z.Provider == command.SourceDataProvider));

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

        //var newParentId = await _dataContext.TryResolveFromDtoRefAsync(
        //    dto.Parent.Ref,
        //    command.SourceDataProvider,
        //    () => _dataContext.AthletePositions,
        //    _logger);

        //if (entity.ParentId != newParentId)
        //{
        //    _logger.LogInformation("Updating ParentId from {Old} to {New}", entity.ParentId, newParentId);
        //    entity.ParentId = newParentId;
        //    updated = true;
        //}

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
