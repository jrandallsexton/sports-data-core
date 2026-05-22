using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Contests;
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

    protected override async Task<bool?> IsCompetitionInProgressAsync(Guid competitionId)
    {
        // Status was lifted off CompetitionBase onto the sport-specific
        // BaseballCompetition in the abstract-status redesign. Loaded
        // independently so the in-progress branch can still gate on
        // IsCompleted. Null return signals "not sourced yet" so the base
        // can throw for a retry instead of silently skipping the live
        // broadcast.
        var status = await _dataContext.Set<BaseballCompetitionStatus>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompetitionId == competitionId);

        return status is null ? null : !status.IsCompleted;
    }

    /// <summary>
    /// Build the full participants list for a baseball play, resolving each
    /// athlete ref to a canonical AthleteSeason ID where possible. The JSON
    /// calls the field "athlete" but the URL is season-scoped
    /// (`/seasons/{year}/athletes/{id}`), so it resolves against
    /// AthleteSeason, not the global AthleteBase. Position resolves the same
    /// way. Per-play statistics refs are captured verbatim — those
    /// pipelines don't materialize canonical entities yet.
    ///
    /// Type strings are stored verbatim (batter, pitcher, onFirst, onSecond,
    /// onThird, fielder, …). Structural info is captured by the resolved
    /// AthleteSeasonId + PositionId — the Type string only matters at read
    /// time when a consumer needs to filter (e.g. ExtractPrimaryAthletes).
    /// Null AthleteSeasonId is acceptable — the play update path re-resolves
    /// on each re-ingest, so transient nulls heal naturally without a
    /// throw-retry loop on every play of a freshly-sourced game.
    /// </summary>
    private async Task<List<BaseballCompetitionPlayParticipant>> BuildParticipantsAsync(
        EspnBaseballEventCompetitionPlayDto dto,
        SourceDataProvider provider)
    {
        var result = new List<BaseballCompetitionPlayParticipant>();
        if (dto.Participants is null || dto.Participants.Count == 0) return result;

        foreach (var participant in dto.Participants)
        {
            Guid? athleteSeasonId = null;
            if (participant.Athlete?.Ref is not null)
            {
                athleteSeasonId = await _dataContext.ResolveIdAsync<AthleteSeason, AthleteSeasonExternalId>(
                    participant.Athlete,
                    provider,
                    () => _dataContext.AthleteSeasons,
                    externalIdsNav: "ExternalIds",
                    key: a => a.Id);
            }

            Guid? positionId = null;
            if (participant.Position?.Ref is not null)
            {
                positionId = await _dataContext.ResolveIdAsync<AthletePosition, AthletePositionExternalId>(
                    participant.Position,
                    provider,
                    () => _dataContext.AthletePositions,
                    externalIdsNav: "ExternalIds",
                    key: p => p.Id);
            }

            result.Add(new BaseballCompetitionPlayParticipant
            {
                Order = participant.Order,
                Type = participant.Type ?? string.Empty,
                AthleteSeasonId = athleteSeasonId,
                PositionId = positionId,
                StatisticsRef = participant.Statistics?.Ref?.ToString()
            });
        }

        return result;
    }

    /// <summary>
    /// Pull the primary pitcher/batter AthleteSeason IDs out of an
    /// already-built participants list. Used to denormalize onto the play
    /// row for cheap live-UI lookup without an extra join.
    /// </summary>
    private static (Guid? AtBatAthleteSeasonId, Guid? PitchingAthleteSeasonId) ExtractPrimaryAthletes(
        List<BaseballCompetitionPlayParticipant> participants)
    {
        Guid? atBat = null;
        Guid? pitching = null;
        foreach (var p in participants)
        {
            if (atBat is null && string.Equals(p.Type, "batter", StringComparison.OrdinalIgnoreCase))
                atBat = p.AthleteSeasonId;
            else if (pitching is null && string.Equals(p.Type, "pitcher", StringComparison.OrdinalIgnoreCase))
                pitching = p.AthleteSeasonId;
        }
        return (atBat, pitching);
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

        var participants = await BuildParticipantsAsync(externalDto, command.SourceDataProvider);
        var (atBatAthleteSeasonId, pitchingAthleteSeasonId) = ExtractPrimaryAthletes(participants);

        _logger.LogInformation(
            "Creating baseball CompetitionPlay. CompetitionId={CompId}, PlayType={PlayType}, ParticipantCount={Count}, AtBatAthleteSeasonId={AtBat}, PitchingAthleteSeasonId={Pitching}",
            competition.Id, externalDto.Type?.Text, participants.Count, atBatAthleteSeasonId, pitchingAthleteSeasonId);

        var play = externalDto.AsBaseballEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId,
            competition.Id,
            teamFranchiseSeasonId,
            atBatAthleteSeasonId,
            pitchingAthleteSeasonId);

        foreach (var participant in participants)
        {
            play.Participants.Add(participant);
        }

        return play;
    }

    protected override async Task PublishSportPlayCompletedAsync(
        ProcessDocumentCommand command,
        CompetitionBase competition,
        CompetitionPlayBase play)
    {
        var baseballPlay = (BaseballCompetitionPlay)play;

        // Runner state still comes from the EventCompetitionSituation
        // pipeline (deferred to Phase 3 of docs/baseball-live-data-plan.md)
        // — emit safe defaults so the UI diamond renderer keeps rendering.
        // The at-bat / pitcher display fields hydrate from the entity here.
        var display = await BaseballPlayCompletedPayloadBuilder.HydrateAsync(
            _dataContext, baseballPlay);

        await _publishEndpoint.Publish(new BaseballPlayCompleted(
            ContestId: competition.ContestId,
            CompetitionId: competition.Id,
            PlayId: baseballPlay.Id,
            PlayDescription: baseballPlay.Text,
            Inning: baseballPlay.PeriodNumber,
            HalfInning: baseballPlay.HalfInning ?? string.Empty,
            AwayScore: baseballPlay.AwayScore,
            HomeScore: baseballPlay.HomeScore,
            Balls: baseballPlay.ResultCountBalls ?? 0,
            Strikes: baseballPlay.ResultCountStrikes ?? 0,
            Outs: baseballPlay.Outs ?? 0,
            RunnerOnFirst: false,
            RunnerOnSecond: false,
            RunnerOnThird: false,
            AtBatAthleteSeasonId: baseballPlay.AtBatAthleteSeasonId,
            AtBatShortName: display.AtBatShortName,
            AtBatPositionAbbreviation: display.AtBatPositionAbbreviation,
            AtBatHeadshotUrl: display.AtBatHeadshotUrl,
            PitchingAthleteSeasonId: baseballPlay.PitchingAthleteSeasonId,
            PitchingShortName: display.PitchingShortName,
            PitchingPositionAbbreviation: display.PitchingPositionAbbreviation,
            PitchingHeadshotUrl: display.PitchingHeadshotUrl,
            Ref: null,
            Sport: command.Sport,
            SeasonYear: command.SeasonYear,
            CorrelationId: command.CorrelationId,
            CausationId: CausationId.Producer.EventCompetitionPlayDocumentProcessor));
    }

    protected override async Task ApplyUpdateAsync(
        CompetitionPlayBase entity,
        ProcessDocumentCommand command,
        EspnBaseballEventCompetitionPlayDto externalDto)
    {
        if (entity is not BaseballCompetitionPlay baseballPlay)
        {
            throw new InvalidOperationException(
                $"Expected BaseballCompetitionPlay but got {entity.GetType().Name}. PlayId={entity.Id}");
        }

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

        var participants = await BuildParticipantsAsync(externalDto, command.SourceDataProvider);
        var (atBatAthleteSeasonId, pitchingAthleteSeasonId) = ExtractPrimaryAthletes(participants);

        // Replace the participants set wholesale: simpler than diffing per-row,
        // and ESPN can re-order or swap participants on a play between fetches
        // (e.g., a substitution row appearing on a later refresh).
        var existingParticipants = await _dataContext.Set<BaseballCompetitionPlayParticipant>()
            .Where(p => p.CompetitionPlayId == baseballPlay.Id)
            .ToListAsync();

        if (existingParticipants.Count > 0)
        {
            _dataContext.Set<BaseballCompetitionPlayParticipant>().RemoveRange(existingParticipants);
        }

        foreach (var participant in participants)
        {
            participant.CompetitionPlayId = baseballPlay.Id;
            await _dataContext.Set<BaseballCompetitionPlayParticipant>().AddAsync(participant);
        }

        _logger.LogInformation(
            "Updating baseball CompetitionPlay. PlayId={PlayId}, ParticipantCount={Count}, AtBatAthleteSeasonId={AtBat}, PitchingAthleteSeasonId={Pitching}",
            entity.Id, participants.Count, atBatAthleteSeasonId, pitchingAthleteSeasonId);

        baseballPlay.StartFranchiseSeasonId = teamFranchiseSeasonId;
        baseballPlay.HalfInning = externalDto.Period?.Type;
        baseballPlay.Outs = externalDto.Outs;
        baseballPlay.Wallclock = externalDto.Wallclock == default ? null : externalDto.Wallclock;
        baseballPlay.IsValid = externalDto.Valid;
        baseballPlay.AtBatAthleteSeasonId = atBatAthleteSeasonId;
        baseballPlay.PitchingAthleteSeasonId = pitchingAthleteSeasonId;
        baseballPlay.AtBatId = externalDto.AtBatId;
        baseballPlay.AtBatPitchNumber = externalDto.AtBatPitchNumber;
        baseballPlay.BatOrder = externalDto.BatOrder;
        baseballPlay.BatsType = externalDto.Bats?.Type;
        baseballPlay.BatsAbbreviation = externalDto.Bats?.Abbreviation;
        baseballPlay.PitchesType = externalDto.Pitches?.Type;
        baseballPlay.PitchesAbbreviation = externalDto.Pitches?.Abbreviation;
        baseballPlay.PitchCoordinateX = externalDto.PitchCoordinate?.X;
        baseballPlay.PitchCoordinateY = externalDto.PitchCoordinate?.Y;
        baseballPlay.HitCoordinateX = externalDto.HitCoordinate?.X;
        baseballPlay.HitCoordinateY = externalDto.HitCoordinate?.Y;
        baseballPlay.PitchTypeId = externalDto.PitchType?.Id;
        baseballPlay.PitchTypeText = externalDto.PitchType?.Text;
        baseballPlay.PitchTypeAbbreviation = externalDto.PitchType?.Abbreviation;
        baseballPlay.PitchVelocity = externalDto.PitchVelocity;
        baseballPlay.PitchCountBalls = externalDto.PitchCount?.Balls;
        baseballPlay.PitchCountStrikes = externalDto.PitchCount?.Strikes;
        baseballPlay.ResultCountBalls = externalDto.ResultCount?.Balls;
        baseballPlay.ResultCountStrikes = externalDto.ResultCount?.Strikes;
        baseballPlay.Trajectory = externalDto.Trajectory;
        baseballPlay.StrikeType = externalDto.StrikeType;
        baseballPlay.SummaryType = externalDto.SummaryType;
        baseballPlay.AwayHits = externalDto.AwayHits;
        baseballPlay.HomeHits = externalDto.HomeHits;
        baseballPlay.AwayErrors = externalDto.AwayErrors;
        baseballPlay.HomeErrors = externalDto.HomeErrors;
        baseballPlay.RbiCount = externalDto.RbiCount;
        baseballPlay.IsDoublePlay = externalDto.DoublePlay;
        baseballPlay.IsTriplePlay = externalDto.TriplePlay;
    }
}
