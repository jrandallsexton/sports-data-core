using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Baseball;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetition)]
public class BaseballEventCompetitionDocumentProcessor<TDataContext> : EventCompetitionDocumentProcessorBase<TDataContext>
    where TDataContext : BaseballDataContext
{
    public BaseballEventCompetitionDocumentProcessor(
        ILogger<BaseballEventCompetitionDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override EspnEventCompetitionDtoBase? DeserializeDto(string document)
        => document.FromJson<EspnBaseballEventCompetitionDto>();

    protected override CompetitionBase CreateEntity(
        EspnEventCompetitionDtoBase dto,
        IGenerateExternalRefIdentities identityGenerator,
        Guid contestId,
        Guid correlationId)
    {
        return dto.AsBaseballEntity(identityGenerator, contestId, correlationId);
    }
}
