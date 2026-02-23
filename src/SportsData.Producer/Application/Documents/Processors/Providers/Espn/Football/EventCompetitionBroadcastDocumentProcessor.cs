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
using SportsData.Core.Infrastructure.DataSources.Espn;

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

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionBroadcastRefToCompetitionRef);

        if (competitionId == null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        var competitionIdValue = competitionId.Value;

        var competition = await _dataContext.Competitions
            .Include(x => x.Broadcasts)
            .FirstOrDefaultAsync(c => c.Id == competitionIdValue);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionIdValue);
            throw new InvalidOperationException($"Competition with Id {competitionIdValue} not found.");
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
                competitionIdValue);

            _dataContext.Broadcasts.AddRange(newBroadcasts);
        }
        else
        {
            _logger.LogInformation("No new broadcasts to add, skipping. CompetitionId={CompId}", competitionIdValue);
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted broadcasts. CompetitionId={CompId}, TotalBroadcasts={Total}",
            competitionIdValue,
            competition.Broadcasts.Count + newBroadcasts.Count);
    }
}