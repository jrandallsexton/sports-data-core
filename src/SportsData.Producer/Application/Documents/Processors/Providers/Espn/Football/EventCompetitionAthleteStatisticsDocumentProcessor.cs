using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

/// <summary>
///  http://sports.core.api.espn.com/v2/sports/football/leagues/college-football/events/401752687/competitions/401752687/competitors/99/roster/4567747/statistics/0?lang=en&region=us
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionAthleteStatistics)]
public class EventCompetitionAthleteStatisticsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public EventCompetitionAthleteStatisticsDocumentProcessor(
        ILogger<EventCompetitionAthleteStatisticsDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator)
    {
    }

    public override async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Processing EventCompetitionAthleteStatistics with {@Command}", command);
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
        var dto = command.Document.FromJson<EspnEventCompetitionAthleteStatisticsDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize document to EventCompetitionAthleteStatisticsDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EventCompetitionAthleteStatisticsDto Ref is null. {@Command}", command);
            return;
        }

        // TODO: Implement processing logic here
        await Task.CompletedTask;
    }
}