using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.GroupSeason)]
public class GroupSeasonDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public GroupSeasonDocumentProcessor(
        ILogger<GroupSeasonDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
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
                await PublishDependencyRequest<string?>(
                    command,
                    dto.Season,
                    parentId: null,
                    DocumentType.Season);

                throw new ExternalDocumentNotSourcedException($"Season {dto.Season.Ref} not found. Requested. Will retry.");
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
                await PublishDependencyRequest<string?>(
                    command,
                    dto.Parent,
                    parentId: null,
                    DocumentType.GroupSeason);

                throw new ExternalDocumentNotSourcedException($"GroupSeason parent {dto.Parent.Ref} not found. Requested. Will retry.");
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
                DocumentType.GroupSeason);
        }

        // NOTE: Future enhancements:
        // - Standings: Can be derived from game results or sourced from ESPN standings endpoint
        // - Links: Additional related resources (if ESPN provides conference-level links)
        // - Teams: Team roster by conference/division
    }

    private async Task HandleExisting()
    {
        _logger.LogWarning("Update detected. Not Implemented");
        await Task.CompletedTask;
    }
}
