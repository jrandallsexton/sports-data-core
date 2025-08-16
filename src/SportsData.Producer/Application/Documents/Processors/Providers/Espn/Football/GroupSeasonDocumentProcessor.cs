using MassTransit;
using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.GroupSeason)]
public class GroupSeasonDocumentProcessor : IProcessDocuments
{
    private readonly ILogger<GroupSeasonDocumentProcessor> _logger;
    private readonly FootballDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public GroupSeasonDocumentProcessor(
        ILogger<GroupSeasonDocumentProcessor> logger,
        FootballDataContext dataContext,
        IPublishEndpoint publishEndpoint,
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
            try
            {
                await ProcessInternal(command);
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
        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            command.Season!.Value,
            command.CorrelationId);

        // seasonRef
        var seasonId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Season,
            command.SourceDataProvider,
            () => _dataContext.Seasons.Include(x => x.ExternalIds).AsNoTracking(),
            _logger);
        entity.SeasonId = seasonId;

        // handle parent?
        var parentId = await _dataContext.TryResolveFromDtoRefAsync(
            dto.Parent,
            command.SourceDataProvider,
            () => _dataContext.GroupSeasons.Include(x => x.ExternalIds).AsNoTracking(),
            _logger);
        entity.ParentId = parentId;

        // spawn child documents?
        if (dto.Children?.Ref is not null)
        {
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: Guid.NewGuid().ToString(),
                ParentId: entity.Id.ToString(),
                Uri: dto.Children.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season!.Value,
                DocumentType: DocumentType.GroupSeason,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.GroupSeasonDocumentProcessor
            ));
        }

        // TODO: standings?

        // teams?
        if (dto.Teams?.Ref is not null)
        {
            await _publishEndpoint.Publish(new DocumentRequested(
                Id: Guid.NewGuid().ToString(),
                ParentId: entity.Id.ToString(),
                Uri: dto.Teams.Ref,
                Sport: command.Sport,
                SeasonYear: command.Season!.Value,
                DocumentType: DocumentType.TeamSeason,
                SourceDataProvider: SourceDataProvider.Espn,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.GroupSeasonDocumentProcessor
            ));
        }

        // TODO: links?

        await _dataContext.GroupSeasons.AddAsync(entity);
        await _dataContext.SaveChangesAsync();
    }

    private async Task HandleExisting()
    {
        _logger.LogWarning("Updated detected. Not Implemented");
        await Task.Delay(100);
        throw new NotImplementedException();
    }
}
