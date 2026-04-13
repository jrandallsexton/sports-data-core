using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
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

    protected override EspnEventCompetitionDtoBase? DeserializeDto(string document)
        => document.FromJson<EspnFootballEventCompetitionDto>();

    protected override CompetitionBase CreateEntity(
        EspnEventCompetitionDtoBase dto,
        IGenerateExternalRefIdentities identityGenerator,
        Guid contestId,
        Guid correlationId)
    {
        return dto.AsFootballEntity(identityGenerator, contestId, correlationId);
    }

    protected override async Task ProcessSportSpecificChildDocuments(
        ProcessDocumentCommand command,
        EspnEventCompetitionDtoBase dto,
        CompetitionBase competition,
        bool isNew)
    {
        if (dto is EspnFootballEventCompetitionDto footballDto)
        {
            if (isNew || ShouldSpawn(DocumentType.EventCompetitionDrive, command))
                await PublishChildDocumentRequest(command, footballDto.Drives, competition.Id, DocumentType.EventCompetitionDrive);
        }
    }
}
