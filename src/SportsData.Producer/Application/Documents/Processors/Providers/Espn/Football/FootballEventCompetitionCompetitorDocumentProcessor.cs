using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionCompetitor)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetitionCompetitor)]
public class FootballEventCompetitionCompetitorDocumentProcessor<TDataContext> : EventCompetitionCompetitorDocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public FootballEventCompetitionCompetitorDocumentProcessor(
        ILogger<FootballEventCompetitionCompetitorDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs) { }

    protected override CompetitionCompetitorBase CreateEntity(
        EspnEventCompetitionCompetitorDto dto,
        Guid competitionId,
        Guid franchiseSeasonId,
        Guid correlationId)
    {
        return dto.AsFootballEntity(
            competitionId,
            franchiseSeasonId,
            _externalRefIdentityGenerator,
            correlationId);
    }
}
