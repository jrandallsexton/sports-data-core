using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionPlay)]
public class BaseballEventCompetitionPlayDocumentProcessor<TDataContext>
    : EventCompetitionPlayDocumentProcessorBase<TDataContext, EspnBaseballEventCompetitionPlayDto>
    where TDataContext : TeamSportDataContext
{
    public BaseballEventCompetitionPlayDocumentProcessor(
        ILogger<BaseballEventCompetitionPlayDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task<bool> IsCompetitionInProgressAsync(Guid competitionId)
    {
        // Status was lifted off CompetitionBase onto the sport-specific
        // BaseballCompetition in the abstract-status redesign. Loaded
        // independently so the in-progress branch can still gate on
        // IsCompleted.
        var status = await _dataContext.Set<BaseballCompetitionStatus>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompetitionId == competitionId);

        return status is not null && !status.IsCompleted;
    }

    protected override async Task<CompetitionPlayBase> BuildNewPlayAsync(
        ProcessDocumentCommand command,
        EspnBaseballEventCompetitionPlayDto externalDto,
        CompetitionBase competition)
    {
        // Baseball plays have a single team ref (no start/end like football).
        Guid? teamFranchiseSeasonId = null;
        if (externalDto.Team?.Ref is not null)
        {
            teamFranchiseSeasonId = await _dataContext.ResolveIdAsync<
                FranchiseSeason, FranchiseSeasonExternalId>(
                externalDto.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons,
                externalIdsNav: "ExternalIds",
                key: fs => fs.Id);
        }

        _logger.LogInformation(
            "Creating baseball CompetitionPlay. CompetitionId={CompId}, PlayType={PlayType}",
            competition.Id, externalDto.Type?.Text);

        return externalDto.AsBaseballEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId,
            competition.Id,
            teamFranchiseSeasonId);
    }

    protected override async Task ApplyUpdateAsync(
        CompetitionPlayBase entity,
        ProcessDocumentCommand command,
        EspnBaseballEventCompetitionPlayDto externalDto)
    {
        Guid? teamFranchiseSeasonId = null;
        if (externalDto.Team?.Ref is not null)
        {
            teamFranchiseSeasonId = await _dataContext.ResolveIdAsync<
                FranchiseSeason, FranchiseSeasonExternalId>(
                externalDto.Team,
                command.SourceDataProvider,
                () => _dataContext.FranchiseSeasons,
                externalIdsNav: "ExternalIds",
                key: fs => fs.Id);
        }

        _logger.LogInformation("Updating baseball CompetitionPlay. PlayId={PlayId}", entity.Id);

        entity.StartFranchiseSeasonId = teamFranchiseSeasonId;
    }
}
