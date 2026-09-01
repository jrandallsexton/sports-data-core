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
using SportsData.Producer.Exceptions;
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
    // Live in-game athlete-stats refresh: minimum minutes between requests
    // for one athlete's cumulative statistics doc within one competition.
    // 3 minutes ≈ drive granularity — plays cluster inside drives, so each
    // involved athlete refreshes about once per drive instead of once per
    // play (a QB would otherwise fan out ~40 ESPN fetches a game against an
    // IP-based rate limiter). Scoring plays bypass the debounce.
    private const int StatsRequestDebounceMinutes = 3;

    private readonly IDateTimeProvider _dateTimeProvider;

    public FootballEventCompetitionPlayDocumentProcessor(
        ILogger<FootballEventCompetitionPlayDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    /// <summary>
    /// Fan out cumulative athlete-statistics requests for the play's
    /// participants. The base calls this only while the competition is IN
    /// PROGRESS, and Provider auto-bypasses its cache for current-season
    /// requests, so each request that survives the debounce is a fresh
    /// ESPN fetch; Provider's content-hash dedupe suppresses the
    /// downstream publish when nothing changed. The debounce watermark
    /// lives on the AthleteCompetition roster row — DB-backed, so it holds
    /// across the 30-worker fleet (two workers racing the same check cost
    /// at most one duplicate fetch, which is not worth locking over).
    /// </summary>
    protected override async Task RequestParticipantStatisticsAsync(
        ProcessDocumentCommand command,
        EspnFootballEventCompetitionPlayDto dto,
        Guid competitionId)
    {
        if (dto.Participants is null || dto.Participants.Count == 0) return;

        var now = _dateTimeProvider.UtcNow();
        var stampedAny = false;

        foreach (var participant in dto.Participants
                     .Where(p => p.Athlete?.Ref is not null && p.Statistics?.Ref is not null)
                     .DistinctBy(p => p.Athlete!.Ref))
        {
            var athleteSeasonId = await _dataContext.ResolveIdAsync<AthleteSeason, AthleteSeasonExternalId>(
                participant.Athlete!,
                command.SourceDataProvider,
                () => _dataContext.AthleteSeasons,
                externalIdsNav: "ExternalIds",
                key: a => a.Id);

            // Participants were dependency-resolved when the play was
            // built; a miss here means the play itself threw for retry
            // before we were called. Defensive skip regardless.
            if (athleteSeasonId is null) continue;

            var rosterRow = await _dataContext.AthleteCompetitions
                .FirstOrDefaultAsync(ac =>
                    ac.CompetitionId == competitionId &&
                    ac.AthleteSeasonId == athleteSeasonId.Value);

            // Roster doc not sourced yet (pre-game cascade still running).
            // A later play retries; no watermark to stamp without a row.
            if (rosterRow is null) continue;

            var due = dto.ScoringPlay
                      || rosterRow.StatsRequestedUtc is null
                      || (now - rosterRow.StatsRequestedUtc.Value).TotalMinutes >= StatsRequestDebounceMinutes;
            if (!due) continue;

            rosterRow.StatsRequestedUtc = now;
            stampedAny = true;

            await PublishChildDocumentRequest(
                command,
                participant.Statistics,
                rosterRow.Id,
                DocumentType.EventCompetitionAthleteStatistics);
        }

        if (stampedAny)
        {
            await _dataContext.SaveChangesAsync();
        }
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

    /// <summary>
    /// Build the per-play participant rows (passer / rusher / receiver / tackler
    /// / …), resolving each athlete ref to a canonical AthleteSeason id and each
    /// position ref to an AthletePosition id. ESPN names the field "athlete" but
    /// the URL is season-scoped (`/seasons/{year}/athletes/{id}`), so it resolves
    /// against AthleteSeason, not the global AthleteBase.
    ///
    /// When a referenced athlete/position isn't sourced yet, request that
    /// dependency and throw <see cref="ExternalDocumentNotSourcedException"/> so
    /// the base retries this play after the dependency lands (the established
    /// dependency-sourcing pattern — see the athlete processor's
    /// ProcessCurrentPosition). Every missing dependency across the participant
    /// set is requested before the single throw, so one retry cycle sources them
    /// all rather than one-per-retry. Deliberately stricter than the baseball
    /// participant processor (which tolerates null): a persisted football play
    /// always has fully-resolved participant attribution.
    /// </summary>
    private async Task<List<FootballCompetitionPlayParticipant>> BuildParticipantsAsync(
        ProcessDocumentCommand command,
        EspnFootballEventCompetitionPlayDto dto)
    {
        var result = new List<FootballCompetitionPlayParticipant>();
        if (dto.Participants is null || dto.Participants.Count == 0) return result;

        var provider = command.SourceDataProvider;
        var anyMissing = false;

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

                if (athleteSeasonId is null)
                {
                    // Empty filter = document only: this request exists purely to
                    // satisfy the participant FK. The AthleteSeason row will persist
                    // (its Athlete parent is a blocking dependency, which still flows),
                    // but its child subtree — headshot, season statistics, notes — will
                    // not spawn. That subtree was the largest single term in the
                    // 2026-08-29 fan-out (~4 documents per unique athlete, across every
                    // division), and nothing on the scoring path consumes it: per-game
                    // stats arrive via EventCompetitionAthleteStatistics from the
                    // roster/leaders/play spawns, and their processor resolves the
                    // AthleteSeason row itself. Season-level data that is genuinely
                    // wanted must be sourced deliberately, not inherited from a play.
                    // See docs/features/athlete-cascade-scoping.md.
                    await PublishDependencyRequest<string?>(
                        command, participant.Athlete, parentId: null, DocumentType.AthleteSeason,
                        includeLinkedDocumentTypes: Array.Empty<DocumentType>());
                    anyMissing = true;
                }
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

                if (positionId is null)
                {
                    // Same FK-only treatment as the AthleteSeason request above.
                    await PublishDependencyRequest<string?>(
                        command, participant.Position, parentId: null, DocumentType.AthletePosition,
                        includeLinkedDocumentTypes: Array.Empty<DocumentType>());
                    anyMissing = true;
                }
            }

            result.Add(new FootballCompetitionPlayParticipant
            {
                Order = participant.Order,
                Type = participant.Type ?? string.Empty,
                AthleteSeasonId = athleteSeasonId,
                PositionId = positionId,
                StatisticsRef = participant.Statistics?.Ref?.ToString()
            });
        }

        if (anyMissing)
        {
            throw new ExternalDocumentNotSourcedException(
                "One or more play participants reference an AthleteSeason/AthletePosition that isn't sourced yet. " +
                "Requested the missing dependencies; will retry this play.");
        }

        return result;
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

        var participants = await BuildParticipantsAsync(command, externalDto);

        _logger.LogInformation(
            "Creating new CompetitionPlay. CompetitionId={CompId}, DriveId={DriveId}, PlayType={PlayType}, ParticipantCount={Count}",
            competition.Id,
            competitionDriveId,
            externalDto.Type?.Text,
            participants.Count);

        var play = externalDto.AsFootballEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId,
            competition.Id,
            competitionDriveId,
            startFranchiseSeasonId,
            endFranchiseSeasonId);

        foreach (var participant in participants)
        {
            play.Participants.Add(participant);
        }

        return play;
    }

    protected override Task PublishSportPlayCompletedAsync(
        ProcessDocumentCommand command,
        CompetitionBase competition,
        CompetitionPlayBase play)
    {
        var footballPlay = (FootballCompetitionPlay)play;

        // Down and distance travel as a PAIR: pairing an end-state distance
        // with a start-state down would describe a snap that never existed.
        // The END state is what the next snap will be; fall back to the
        // start pair only when the end down is absent entirely.
        var hasEndSnapState = footballPlay.EndDown is not null;
        var down = hasEndSnapState ? footballPlay.EndDown : footballPlay.StartDown;
        var distance = hasEndSnapState ? footballPlay.EndDistance : footballPlay.StartDistance;

        return _publishEndpoint.Publish(new FootballPlayCompleted(
            ContestId: competition.ContestId,
            CompetitionId: competition.Id,
            PlayId: footballPlay.Id,
            PlayDescription: footballPlay.Text,
            Period: $"Q{footballPlay.PeriodNumber}",
            Clock: footballPlay.ClockDisplayValue ?? string.Empty,
            AwayScore: footballPlay.AwayScore,
            HomeScore: footballPlay.HomeScore,
            // END-of-play team: who lines up for the NEXT snap, matching the
            // down/distance below (also end-state). Start differs from end on
            // 1 in 6 plays — every punt, turnover and change of possession —
            // so publishing Start credited the punting team with the ball.
            PossessionFranchiseSeasonId:
                footballPlay.EndFranchiseSeasonId ?? footballPlay.StartFranchiseSeasonId,
            IsScoringPlay: footballPlay.ScoringPlay,
            ScoringPlayType: footballPlay.ScoringTypeName,
            BallOnYardLine: footballPlay.EndYardLine ?? footballPlay.StartYardLine,
            Down: down,
            Distance: distance,
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

        // Resolve participants BEFORE mutating the tracked entity. A missing
        // dependency throws ExternalDocumentNotSourcedException, and the base's
        // retry handler calls SaveChangesAsync — so any tracked mutation made
        // before the throw would be persisted despite "withhold for retry". By
        // building participants first, a throw here leaves the tracked play
        // completely unmodified.
        var participants = await BuildParticipantsAsync(command, externalDto);

        var createdUtc = footballPlay.CreatedUtc;
        var createdBy = footballPlay.CreatedBy;
        _dataContext.Entry(footballPlay).CurrentValues.SetValues(mapped);
        footballPlay.CreatedUtc = createdUtc;
        footballPlay.CreatedBy = createdBy;

        // Participants live in a separate table, so SetValues (scalars only) can't
        // refresh them. Replace the set wholesale: simpler than diffing per-row, and
        // ESPN can re-order or swap participants on a play between fetches. Mirrors
        // the baseball processor.
        var existingParticipants = await _dataContext.Set<FootballCompetitionPlayParticipant>()
            .Where(p => p.CompetitionPlayId == footballPlay.Id)
            .ToListAsync();

        if (existingParticipants.Count > 0)
        {
            _dataContext.Set<FootballCompetitionPlayParticipant>().RemoveRange(existingParticipants);
        }

        foreach (var participant in participants)
        {
            participant.CompetitionPlayId = footballPlay.Id;
            await _dataContext.Set<FootballCompetitionPlayParticipant>().AddAsync(participant);
        }
    }
}
