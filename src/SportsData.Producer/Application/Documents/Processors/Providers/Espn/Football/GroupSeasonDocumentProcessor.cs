using MassTransit;

using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Conferences;
using SportsData.Core.Eventing.Events.Images;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

using Group = SportsData.Producer.Infrastructure.Data.Entities.Group;

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
        if (dto == null || dto.Ref == null || !command.Season.HasValue)
        {
            _logger.LogError("Invalid GroupSeason document. {@Command}", command);
            return;
        }

        var group = await _dataContext.Groups
            .Include(g => g.Seasons)
            .Include(g => g.ExternalIds)
            .FirstOrDefaultAsync(g =>
                g.ExternalIds.Any(x => x.Provider == command.SourceDataProvider &&
                                       x.Value == dto.Id.ToString()));

        if (group is null)
        {
            await HandleNewGroupAndSeasonAsync(dto, command);
        }
        else
        {
            _logger.LogInformation("Group already exists. Checking for missing season.");
            await AddSeasonIfMissingAsync(group, dto, command);
        }
    }

    private async Task HandleNewGroupAndSeasonAsync(
        EspnGroupSeasonDto dto,
        ProcessDocumentCommand command)
    {
        var groupId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();

        var group = dto.AsEntity(_externalRefIdentityGenerator, groupId, command.CorrelationId);
        var season = dto.AsEntity(_externalRefIdentityGenerator, groupId, seasonId, command.Season!.Value, command.CorrelationId);

        await _dataContext.Groups.AddAsync(group);
        await _dataContext.SaveChangesAsync();

        await _dataContext.GroupSeasons.AddAsync(season);

        if (dto.Logos is { Count: > 0 })
        {
            var logoEvents = dto.Logos.Select(logo => new ProcessImageRequest(
                logo.Href,
                Guid.NewGuid(),
                seasonId,
                $"{seasonId}.png",
                command.Sport,
                command.Season,
                command.DocumentType,
                command.SourceDataProvider,
                0,
                0,
                null,
                command.CorrelationId,
                CausationId.Producer.GroupBySeasonDocumentProcessor));

            await _publishEndpoint.PublishBatch(logoEvents);
        }

        await _publishEndpoint.Publish(new ConferenceCreated(
            group.ToCanonicalModel(),
            command.CorrelationId,
            CausationId.Producer.GroupBySeasonDocumentProcessor));

        await _publishEndpoint.Publish(new ConferenceSeasonCreated(
            season.ToCanonicalModel(),
            command.CorrelationId,
            CausationId.Producer.GroupBySeasonDocumentProcessor));

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Created new group and season. GroupId={GroupId}, SeasonId={SeasonId}", groupId, seasonId);
    }

    private async Task AddSeasonIfMissingAsync(
        Group group,
        EspnGroupSeasonDto dto,
        ProcessDocumentCommand command)
    {
        var seasonYear = command.Season!.Value;

        if (group.Seasons.Any(s => s.Season == seasonYear))
        {
            _logger.LogInformation("GroupSeason already exists. Skipping creation.");
            return;
        }

        var newSeason = dto.AsEntity(_externalRefIdentityGenerator, group.Id, Guid.NewGuid(), seasonYear, command.CorrelationId);
        group.Seasons.Add(newSeason);
        await _dataContext.GroupSeasons.AddAsync(newSeason);

        await _dataContext.SaveChangesAsync();

        await _publishEndpoint.Publish(new ConferenceSeasonCreated(
            newSeason.ToCanonicalModel(),
            command.CorrelationId,
            CausationId.Producer.GroupBySeasonDocumentProcessor));

        _logger.LogInformation("Added missing GroupSeason for existing Group. GroupId={GroupId}, Season={Season}",
            group.Id, seasonYear);
    }
}
