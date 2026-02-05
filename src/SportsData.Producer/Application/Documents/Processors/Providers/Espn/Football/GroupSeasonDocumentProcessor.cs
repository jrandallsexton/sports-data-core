using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Config;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.GroupSeason)]
public class GroupSeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    private readonly DocumentProcessingConfig _config;

    public GroupSeasonDocumentProcessor(
        ILogger<GroupSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        DocumentProcessingConfig config)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _config = config;
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = command.CorrelationId
        }))
        {
            try
            {
                await ProcessInternal(command);
            }
            catch (ExternalDocumentNotSourcedException retryEx)
            {
                _logger.LogWarning(retryEx, "Dependency not ready. Will retry later.");
                var docCreated = command.ToDocumentCreated(command.AttemptCount + 1);
                await _publishEndpoint.Publish(docCreated);
                await _dataContext.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnGroupSeasonDto>();
        if (dto is null)
        {
            _logger.LogError("Invalid GroupSeason document. {@Command}", command);
            return;
        }

        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var groupSeason = await _dataContext.GroupSeasons
            .Include(gs => gs.ExternalIds)
            .FirstOrDefaultAsync(gs => gs.Id == identity.CanonicalId);

        if (groupSeason is null)
        {
            await HandleNew(dto, command);
        }
        else
        {
            await HandleExisting();
        }
    }

    private async Task HandleNew(
        EspnGroupSeasonDto dto,
        ProcessDocumentCommand command)
    {
        var groupSeasonEntity = dto.AsEntity(
            _externalRefIdentityGenerator,
            command.Season!.Value,
            command.CorrelationId);

        // seasonRef
        if (dto.Season?.Ref is not null)
        {
            var seasonId = await _dataContext.ResolveIdAsync<
                Season, SeasonExternalId>(
                dto.Season,
                command.SourceDataProvider,
                () => _dataContext.Seasons,
                externalIdsNav: "ExternalIds",
                key: s => s.Id);

            if (seasonId is null)
            {
                if (!_config.EnableDependencyRequests)
                {
                    _logger.LogWarning(
                        "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                        DocumentType.Season,
                        nameof(GroupSeasonDocumentProcessor<TDataContext>),
                        dto.Season.Ref);
                    throw new ExternalDocumentNotSourcedException(
                        $"Season {dto.Season.Ref} not found. Will retry when available.");
                }
                else
                {
                    // Legacy mode: keep existing DocumentRequested logic
                    _logger.LogWarning(
                        "Season not found. Raising DocumentRequested (override mode). SeasonRef={SeasonRef}",
                        dto.Season.Ref);
                    
                    await PublishChildDocumentRequest<string?>(
                        command,
                        dto.Season,
                        parentId: null,
                        DocumentType.Season,
                        CausationId.Producer.GroupSeasonDocumentProcessor);
                    
                    await _dataContext.SaveChangesAsync();

                    throw new ExternalDocumentNotSourcedException($"Season {dto.Season.Ref} not found. Will retry.");
                }
            }
            groupSeasonEntity.SeasonId = seasonId;
        }

        // handle parent
        if (dto.Parent?.Ref is not null)
        {
            var parentId = await _dataContext.ResolveIdAsync<
                GroupSeason, GroupSeasonExternalId>(
                dto.Parent,
                command.SourceDataProvider,
                () => _dataContext.GroupSeasons,
                externalIdsNav: "ExternalIds",
                key: gs => gs.Id);

            if (parentId is null)
            {
                if (!_config.EnableDependencyRequests)
                {
                    _logger.LogWarning(
                        "Missing dependency: {MissingDependencyType}. Processor: {ProcessorName}. Will retry. EnableDependencyRequests=false. Ref={Ref}",
                        DocumentType.GroupSeason,
                        nameof(GroupSeasonDocumentProcessor<TDataContext>),
                        dto.Parent.Ref);
                    throw new ExternalDocumentNotSourcedException(
                        $"GroupSeason {dto.Parent.Ref} not found. Will retry.");
                }
                else
                {
                    // Legacy mode: keep existing DocumentRequested logic
                    _logger.LogWarning(
                        "Parent GroupSeason not found. Raising DocumentRequested (override mode). ParentRef={ParentRef}",
                        dto.Parent.Ref);
                    
                    await PublishChildDocumentRequest<string?>(
                        command,
                        dto.Parent,
                        parentId: null,
                        DocumentType.GroupSeason,
                        CausationId.Producer.GroupSeasonDocumentProcessor);
                    
                    await _dataContext.SaveChangesAsync();

                    throw new ExternalDocumentNotSourcedException($"GroupSeason {dto.Parent.Ref} not found. Will retry.");
                }
            }
            groupSeasonEntity.ParentId = parentId;
        }

        await ProcessChildren(dto, groupSeasonEntity, command);

        await _dataContext.GroupSeasons.AddAsync(groupSeasonEntity);
        await _dataContext.SaveChangesAsync();
    }

    private async Task ProcessChildren(
        EspnGroupSeasonDto dto,
        GroupSeason groupSeasonEntity,
        ProcessDocumentCommand command)
    {
        if (dto.Children?.Ref is not null)
        {
            await PublishChildDocumentRequest(
                command,
                dto.Children,
                groupSeasonEntity.Id,
                DocumentType.GroupSeason,
                CausationId.Producer.GroupSeasonDocumentProcessor);
        }

        // NOTE: Future enhancements:
        // - Standings: Can be derived from game results or sourced from ESPN standings endpoint
        // - Links: Additional related resources (if ESPN provides conference-level links)
        // - Teams: Team roster by conference/division (commented code above shows structure)
    }

    private async Task HandleExisting()
    {
        _logger.LogWarning("Update detected. Not Implemented");
        await Task.CompletedTask;
    }
}
