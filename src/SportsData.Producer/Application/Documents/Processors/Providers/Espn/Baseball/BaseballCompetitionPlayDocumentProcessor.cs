using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionPlay)]
public class BaseballCompetitionPlayDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    public BaseballCompetitionPlayDocumentProcessor(
        ILogger<BaseballCompetitionPlayDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<EspnEventCompetitionPlayDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize EspnEventCompetitionPlayDto.");
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionPlayDto Ref is null.");
            return;
        }

        if (!command.SeasonYear.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            return;
        }

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionPlayRefToCompetitionRef);

        if (competitionId == null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        var competitionIdValue = competitionId.Value;

        var competition = await _dataContext.Competitions
            .Include(c => c.Status)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionIdValue);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionIdValue);
            throw new InvalidOperationException($"Competition with ID {competitionIdValue} does not exist.");
        }

        // Baseball plays have team but no start/end with team refs
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

        var playIdentity = _externalRefIdentityGenerator.Generate(externalDto.Ref);

        var entity = await _dataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Id == playIdentity.CanonicalId);

        if (entity is null)
        {
            await ProcessNew(command, externalDto, competition, teamFranchiseSeasonId);
        }
        else
        {
            await ProcessExisting(entity, teamFranchiseSeasonId);
        }
    }

    private async Task ProcessNew(
        ProcessDocumentCommand command,
        EspnEventCompetitionPlayDto externalDto,
        Competition competition,
        Guid? teamFranchiseSeasonId)
    {
        _logger.LogInformation(
            "Creating baseball CompetitionPlay. CompetitionId={CompId}, PlayType={PlayType}",
            competition.Id, externalDto.Type?.Text);

        var play = externalDto.AsEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId,
            competition.Id,
            driveId: null,
            startFranchiseSeasonId: teamFranchiseSeasonId,
            endFranchiseSeasonId: null);

        if (competition.Status is not null && !competition.Status.IsCompleted)
        {
            await _publishEndpoint.Publish(new CompetitionPlayCompleted(
                CompetitionPlayId: play.Id,
                CompetitionId: competition.Id,
                ContestId: competition.ContestId,
                PlayDescription: play.Text,
                Ref: null,
                Sport: command.Sport,
                SeasonYear: command.SeasonYear,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionPlayDocumentProcessor));
        }

        await _dataContext.CompetitionPlays.AddAsync(play);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted baseball CompetitionPlay. CompetitionId={CompId}, PlayId={PlayId}, Sequence={Sequence}",
            competition.Id, play.Id, play.SequenceNumber);
    }

    private async Task ProcessExisting(
        CompetitionPlay entity,
        Guid? teamFranchiseSeasonId)
    {
        _logger.LogInformation("Updating baseball CompetitionPlay. PlayId={PlayId}", entity.Id);

        entity.StartFranchiseSeasonId = teamFranchiseSeasonId;

        await _dataContext.SaveChangesAsync();
    }
}
