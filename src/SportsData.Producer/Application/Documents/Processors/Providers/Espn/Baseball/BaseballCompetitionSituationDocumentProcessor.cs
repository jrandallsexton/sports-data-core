using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionSituation)]
public class BaseballCompetitionSituationDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public BaseballCompetitionSituationDocumentProcessor(
        ILogger<BaseballCompetitionSituationDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnEventCompetitionSituationDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionSituationDto.");
            return;
        }

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionSituationRefToCompetitionRef);

        if (competitionId == null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        var competitionIdValue = competitionId.Value;

        // Resolve LastPlay if available
        Guid? lastPlayId = null;
        if (dto.LastPlay?.Ref is not null)
        {
            var lastPlayIdentity = _externalRefIdentityGenerator.Generate(dto.LastPlay.Ref);

            var lastPlay = await _dataContext.CompetitionPlays
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == lastPlayIdentity.CanonicalId);

            if (lastPlay == null)
            {
                await PublishChildDocumentRequest(
                    command,
                    dto.LastPlay,
                    competitionIdValue,
                    DocumentType.EventCompetitionPlay);

                throw new ExternalDocumentNotSourcedException(
                    $"Last Play {dto.LastPlay.Ref} not found. Requesting. Will retry.");
            }

            lastPlayId = lastPlay.Id;
        }

        var identity = _externalRefIdentityGenerator.Generate(dto.Ref);

        var exists = await _dataContext.CompetitionSituations
            .AsNoTracking()
            .AnyAsync(x => x.Id == identity.CanonicalId);

        if (exists)
        {
            _logger.LogInformation("CompetitionSituation already exists, skipping. Id={Id}", identity.CanonicalId);
            return;
        }

        // Store baseball situation using the shared entity.
        // Football-specific fields (Down, Distance, YardLine) set to 0.
        // Baseball-specific data (balls, strikes, outs, runners) is not yet
        // captured in the entity — will be addressed with sport-specific
        // situation entities in a future refactor.
        var entity = new CompetitionSituation
        {
            Id = identity.CanonicalId,
            CompetitionId = competitionIdValue,
            LastPlayId = lastPlayId,
            Down = 0,
            Distance = 0,
            YardLine = 0,
            IsRedZone = false,
            AwayTimeouts = 0,
            HomeTimeouts = 0,
            CreatedBy = command.CorrelationId,
            CreatedUtc = DateTime.UtcNow
        };

        await _dataContext.CompetitionSituations.AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted baseball CompetitionSituation. CompetitionId={CompId}, SituationId={SituationId}",
            competitionIdValue, entity.Id);
    }
}
