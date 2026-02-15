using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionBroadcast)]
public class EventCompetitionBroadcastDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionBroadcastDocumentProcessor(
        ILogger<EventCompetitionBroadcastDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<EspnEventCompetitionBroadcastDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionBroadcastDto.");
            return;
        }

        if (string.IsNullOrEmpty(command.ParentId))
        {
            _logger.LogError("ParentId not provided. Cannot process broadcast for null CompetitionId.");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("Invalid ParentId format for CompetitionId. ParentId={ParentId}", command.ParentId);
            return;
        }

        var competition = await _dataContext.Competitions
            .Include(x => x.Broadcasts)
            .FirstOrDefaultAsync(c => c.Id == competitionId);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionId);
            throw new InvalidOperationException($"Competition with Id {competitionId} not found.");
        }

        var existingKeys = new HashSet<string>(
            competition.Broadcasts.Select(x => $"{x.TypeId}|{x.Channel}|{x.Slug}".ToLowerInvariant())
        );

        var newBroadcasts = externalDto.Items
            .Where(item =>
            {
                var key = $"{item.Type.Id}|{item.Channel}|{item.Slug}".ToLowerInvariant();
                return !existingKeys.Contains(key);
            })
            .Select(item => item.AsEntity(competition.Id))
            .ToList();

        if (newBroadcasts.Count > 0)
        {
            _logger.LogInformation("Adding {Count} new broadcasts. CompetitionId={CompId}", 
                newBroadcasts.Count, 
                competitionId);

            _dataContext.Broadcasts.AddRange(newBroadcasts);
        }
        else
        {
            _logger.LogInformation("No new broadcasts to add, skipping. CompetitionId={CompId}", competitionId);
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted broadcasts. CompetitionId={CompId}, TotalBroadcasts={Total}", 
            competitionId,
            competition.Broadcasts.Count + newBroadcasts.Count);
    }
}