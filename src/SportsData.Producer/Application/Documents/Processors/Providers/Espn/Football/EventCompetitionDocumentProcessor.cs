using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetition)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetition)]
public class EventCompetitionDocumentProcessor<TDataContext> : EventCompetitionDocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public EventCompetitionDocumentProcessor(
        ILogger<EventCompetitionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override CompetitionBase CreateEntity(
        EspnEventCompetitionDto dto,
        IGenerateExternalRefIdentities identityGenerator,
        Guid contestId,
        Guid correlationId)
    {
        return dto.AsFootballEntity(identityGenerator, contestId, correlationId);
    }
}
