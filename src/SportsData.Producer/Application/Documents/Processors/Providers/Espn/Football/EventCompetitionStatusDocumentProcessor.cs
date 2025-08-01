using MassTransit;
using Microsoft.EntityFrameworkCore;
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionStatus)]
public class EventCompetitionStatusDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<EventCompetitionStatusDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _dataContext;
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly IGenerateExternalRefIdentities _externalRefIdentityGenerator;

    public EventCompetitionStatusDocumentProcessor(
        ILogger<EventCompetitionStatusDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
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
            _logger.LogInformation("Began with {@command}", command);
            await ProcessInternal(command);
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var publishEvent = false;

        var dto = command.Document.FromJson<EspnEventCompetitionStatusDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnEventCompetitionStatusDto. {@Command}", command);
            return; // terminal failure — don't retry
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionStatusDto Ref is null or empty. {@Command}", command);
            return; // terminal failure — don't retry
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("ParentId is missing or invalid for CompetitionStatus: {parentId}", command.ParentId);
            throw new InvalidOperationException("CompetitionId (ParentId) is required to process CompetitionStatus");
        }

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competitionId,
            command.CorrelationId);

        var existing = await _dataContext.CompetitionStatuses
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionId);

        if (existing is not null)
        {
            publishEvent = existing.StatusTypeName != dto.Type.Name;
            _dataContext.CompetitionStatuses.Remove(existing);
        }

        if (publishEvent)
        {
            await _publishEndpoint.Publish(new CompetitionStatusChanged(
                competitionId,
                entity.StatusTypeName,
                command.CorrelationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }

        await _dataContext.CompetitionStatuses.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionStatus for competition {competitionId}", competitionId);
    }

}