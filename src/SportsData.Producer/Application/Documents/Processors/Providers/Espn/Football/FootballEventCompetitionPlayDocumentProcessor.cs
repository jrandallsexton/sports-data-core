using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests.Football;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Contracts;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionPlay)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetitionPlay)]
public class FootballEventCompetitionPlayDocumentProcessor<TDataContext>
    : EventCompetitionPlayDocumentProcessorBase<TDataContext, EspnFootballEventCompetitionPlayDto>
    where TDataContext : FootballDataContext
{
    public FootballEventCompetitionPlayDocumentProcessor(
        ILogger<FootballEventCompetitionPlayDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    /// <summary>
    /// Resolve a FranchiseSeason canonical id from a team ref. Football
    /// plays carry both `Start.Team` and `End.Team` and the create+update
    /// branches both need to look both up — this helper keeps the four
    /// call sites identical so the lookup contract can't drift.
    /// </summary>
    private Task<Guid?> ResolveFranchiseSeasonIdAsync(IHasRef teamRef, SourceDataProvider provider)
        => _dataContext.ResolveIdAsync<FranchiseSeason, FranchiseSeasonExternalId>(
            teamRef,
            provider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

    protected override async Task<bool?> IsCompetitionInProgressAsync(Guid competitionId)
    {
        // Status was lifted off CompetitionBase onto the sport-specific
        // FootballCompetition in the abstract-status redesign. Loaded
        // independently so the live/post-game branch can still gate on
        // IsCompleted. Null return signals "not sourced yet" so the base
        // can throw for a retry instead of silently skipping the live
        // broadcast.
        var status = await _dataContext.Set<FootballCompetitionStatus>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompetitionId == competitionId);

        return status is null ? null : !status.IsCompleted;
    }

    protected override async Task<CompetitionPlayBase> BuildNewPlayAsync(
        ProcessDocumentCommand command,
        EspnFootballEventCompetitionPlayDto externalDto,
        CompetitionBase competition)
    {
        Guid? competitionDriveId = null;

        if (command.PropertyBag.TryGetValue("CompetitionDriveId", out var value)
            && Guid.TryParse(value, out var driveId))
        {
            competitionDriveId = driveId;
        }

        var startFranchiseSeasonId = await ResolveFranchiseSeasonIdAsync(
            externalDto.Start.Team, command.SourceDataProvider);

        var endFranchiseSeasonId = await ResolveFranchiseSeasonIdAsync(
            externalDto.End.Team, command.SourceDataProvider);

        _logger.LogInformation(
            "Creating new CompetitionPlay. CompetitionId={CompId}, DriveId={DriveId}, PlayType={PlayType}",
            competition.Id,
            competitionDriveId,
            externalDto.Type?.Text);

        return externalDto.AsFootballEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId,
            competition.Id,
            competitionDriveId,
            startFranchiseSeasonId,
            endFranchiseSeasonId);
    }

    protected override Task PublishSportSpecificStateAsync(
        ProcessDocumentCommand command,
        CompetitionBase competition,
        CompetitionPlayBase play)
    {
        var footballPlay = (FootballCompetitionPlay)play;

        return _publishEndpoint.Publish(new FootballContestStateChanged(
            ContestId: competition.ContestId,
            Period: $"Q{footballPlay.PeriodNumber}",
            Clock: footballPlay.ClockDisplayValue ?? string.Empty,
            AwayScore: footballPlay.AwayScore,
            HomeScore: footballPlay.HomeScore,
            PossessionFranchiseSeasonId: footballPlay.StartFranchiseSeasonId,
            IsScoringPlay: footballPlay.ScoringPlay,
            BallOnYardLine: footballPlay.EndYardLine ?? footballPlay.StartYardLine,
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.SeasonYear,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionPlayDocumentProcessor));
    }

    protected override async Task ApplyUpdateAsync(
        CompetitionPlayBase entity,
        ProcessDocumentCommand command,
        EspnFootballEventCompetitionPlayDto externalDto)
    {
        if (entity is not FootballCompetitionPlay footballPlay)
        {
            throw new InvalidOperationException(
                $"Expected FootballCompetitionPlay but got {entity.GetType().Name}. PlayId={entity.Id}");
        }

        Guid? competitionDriveId = null;

        if (command.PropertyBag.TryGetValue("CompetitionDriveId", out var value)
            && Guid.TryParse(value, out var driveId))
        {
            competitionDriveId = driveId;
        }

        var startFranchiseSeasonId = await ResolveFranchiseSeasonIdAsync(
            externalDto.Start.Team, command.SourceDataProvider);

        var endFranchiseSeasonId = await ResolveFranchiseSeasonIdAsync(
            externalDto.End.Team, command.SourceDataProvider);

        _logger.LogInformation(
            "Updating CompetitionPlay. PlayId={PlayId}, DriveId={DriveId}",
            entity.Id,
            competitionDriveId);

        footballPlay.StartFranchiseSeasonId = startFranchiseSeasonId;
        footballPlay.EndFranchiseSeasonId = endFranchiseSeasonId;
        footballPlay.DriveId = competitionDriveId;
    }
}
