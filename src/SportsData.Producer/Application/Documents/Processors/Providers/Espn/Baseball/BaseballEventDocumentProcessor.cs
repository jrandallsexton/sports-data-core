
using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.Event)]
public class BaseballEventDocumentProcessor<TDataContext> : EventDocumentProcessorBase<TDataContext>
    where TDataContext : BaseballDataContext
{
    public BaseballEventDocumentProcessor(
        ILogger<BaseballEventDocumentProcessor<TDataContext>> logger,
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
        return dto.AsBaseballEntity(
            identityGenerator, sport, seasonYear, seasonWeekId, seasonPhaseId, correlationId);
    }
}
