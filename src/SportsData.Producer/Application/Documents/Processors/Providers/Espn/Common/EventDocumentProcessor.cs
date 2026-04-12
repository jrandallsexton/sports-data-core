using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.Event)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.Event)]
public class EventDocumentProcessor<TDataContext> : EventDocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public EventDocumentProcessor(
        ILogger<EventDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override ContestBase CreateEntity(
        EspnEventDto dto,
        IGenerateExternalRefIdentities identityGenerator,
        Sport sport,
        int seasonYear,
        Guid? seasonWeekId,
        Guid seasonPhaseId,
        Guid correlationId)
    {
        return dto.AsFootballEntity(
            identityGenerator, sport, seasonYear, seasonWeekId, seasonPhaseId, correlationId);
    }
}
