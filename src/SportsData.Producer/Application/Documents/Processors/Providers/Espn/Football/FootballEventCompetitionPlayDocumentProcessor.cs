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

    protected override Task PublishSportPlayCompletedAsync(
        ProcessDocumentCommand command,
        CompetitionBase competition,
        CompetitionPlayBase play)
    {
        var footballPlay = (FootballCompetitionPlay)play;

        return _publishEndpoint.Publish(new FootballPlayCompleted(
            ContestId: competition.ContestId,
            CompetitionId: competition.Id,
            PlayId: footballPlay.Id,
            PlayDescription: footballPlay.Text,
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
            "Updating CompetitionPlay (full remap). PlayId={PlayId}, DriveId={DriveId}",
            entity.Id,
            competitionDriveId);

        // Full remap on reprocess/replay: previously this set only the three FK
        // fields, so re-sourcing an existing play never refreshed the rest (and
        // left newly-captured columns null). Build a fresh entity from the same
        // create mapper and copy its scalar values onto the tracked one — so every
        // mapped field is refreshed and no field can be missed here. SetValues
        // touches scalars only, so the ExternalIds navigation is untouched; the
        // canonical Id is identical (same ref), and the audit columns are
        // preserved explicitly.
        var mapped = externalDto.AsFootballEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId,
            footballPlay.CompetitionId,
            competitionDriveId,
            startFranchiseSeasonId,
            endFranchiseSeasonId);

        // SetValues copies the key too; the remap must derive the same canonical id
        // as the tracked entity (both come from the same play ref). Fail loud rather
        // than silently mutate the primary key if that invariant is ever violated.
        if (mapped.Id != footballPlay.Id)
        {
            throw new InvalidOperationException(
                $"Play identity mismatch on update: remapped id {mapped.Id} != tracked id {footballPlay.Id}. " +
                $"Ref={externalDto.Ref}");
        }

        var createdUtc = footballPlay.CreatedUtc;
        var createdBy = footballPlay.CreatedBy;
        _dataContext.Entry(footballPlay).CurrentValues.SetValues(mapped);
        footballPlay.CreatedUtc = createdUtc;
        footballPlay.CreatedBy = createdBy;
    }
}
