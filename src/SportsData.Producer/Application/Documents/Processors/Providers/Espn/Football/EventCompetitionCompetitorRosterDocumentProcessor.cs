using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Documents;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitorRoster)]
public class EventCompetitionCompetitorRosterDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionCompetitorRosterDocumentProcessor(
        ILogger<EventCompetitionCompetitorRosterDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["DocumentType"] = command.DocumentType,
                   ["Season"] = command.Season ?? 0,
                   ["CompetitorId"] = command.ParentId ?? "Unknown"
               }))
        {
            _logger.LogInformation("EventCompetitionCompetitorRosterDocumentProcessor started. Ref={Ref}, UrlHash={UrlHash}",
                command.GetDocumentRef(),
                command.UrlHash);

            try
            {
                await ProcessInternal(command);

                _logger.LogInformation("EventCompetitionCompetitorRosterDocumentProcessor completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionCompetitorRosterDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionCompetitorRosterDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionCompetitorRosterDto.");
            return;
        }

        if (string.IsNullOrWhiteSpace(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionCompetitorRosterDto Ref is null.");
            return;
        }

        if (string.IsNullOrWhiteSpace(command.ParentId))
        {
            _logger.LogError("Command missing ParentId for CompetitorId.");
            return;
        }

        _logger.LogInformation("Processing roster with {EntryCount} entries. CompetitorId={CompetitorId}",
            dto.Entries.Count,
            command.ParentId);

        // Publish child document requests for each athlete's statistics
        foreach (var entry in dto.Entries)
        {
            if (entry.Statistics?.Ref is null)
            {
                _logger.LogDebug("Athlete {AthleteId} ({DisplayName}) has no statistics ref, skipping.",
                    entry.PlayerId,
                    entry.DisplayName);
                continue;
            }

            _logger.LogDebug("Publishing child request for athlete statistics. Athlete={DisplayName}, StatRef={StatRef}",
                entry.DisplayName,
                entry.Statistics.Ref);

            await PublishChildDocumentRequest<string?>(
                command,
                entry.Statistics,
                null, // Stats document is self-contained with athlete and competition refs
                DocumentType.EventCompetitionAthleteStatistics,
                CausationId.Producer.EventCompetitionCompetitorRosterDocumentProcessor);
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Completed processing roster. PublishedStatisticsRequests={Count}, CompetitorId={CompetitorId}",
            dto.Entries.Count(e => e.Statistics?.Ref != null),
            command.ParentId);
    }
}
