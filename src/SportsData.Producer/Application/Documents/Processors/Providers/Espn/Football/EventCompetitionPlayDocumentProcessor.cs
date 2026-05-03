using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Eventing.Events.Contests.Football;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Football;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;
using SportsData.Producer.Infrastructure.Data.Football;
using SportsData.Producer.Infrastructure.Data.Football.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionPlay)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetitionPlay)]
public class EventCompetitionPlayDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : FootballDataContext
{
    public EventCompetitionPlayDocumentProcessor(
        ILogger<EventCompetitionPlayDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<EspnFootballEventCompetitionPlayDto>();
            
        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize EspnFootballEventCompetitionPlayDto.");
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnFootballEventCompetitionPlayDto Ref is null.");
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

        Guid? competitionDriveId = null;
            
        if (command.PropertyBag.TryGetValue("CompetitionDriveId", out var value))
        {
            if (Guid.TryParse(value, out var driveId))
            {
                competitionDriveId = driveId;
            }
        }

        var competition = await _dataContext.Competitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionIdValue);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionIdValue);
            throw new InvalidOperationException($"Competition with ID {competitionIdValue} does not exist.");
        }

        // Status was lifted off CompetitionBase onto the sport-specific
        // FootballCompetition in the abstract-status redesign. Loaded
        // independently so the live/post-game branch below can still
        // gate on IsCompleted.
        var competitionStatus = await _dataContext.Set<FootballCompetitionStatus>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompetitionId == competitionIdValue);

        var startFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            externalDto.Start.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        var endFranchiseSeasonId = await _dataContext.ResolveIdAsync<
            FranchiseSeason, FranchiseSeasonExternalId>(
            externalDto.End.Team,
            command.SourceDataProvider,
            () => _dataContext.FranchiseSeasons,
            externalIdsNav: "ExternalIds",
            key: fs => fs.Id);

        var playIdentity = _externalRefIdentityGenerator.Generate(externalDto.Ref);

        var entity = await _dataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Id == playIdentity.CanonicalId);

        if (entity is null)
        {
            _logger.LogInformation("Processing new CompetitionPlayBase entity. Ref={Ref}", externalDto.Ref);
            await ProcessNewEntity(
                command,
                externalDto,
                competition,
                competitionStatus,
                competitionDriveId,
                startFranchiseSeasonId,
                endFranchiseSeasonId);
        }
        else
        {
            _logger.LogInformation("Processing CompetitionPlay update. PlayId={PlayId}, Ref={Ref}", entity.Id, externalDto.Ref);
            await ProcessUpdate(
                command,
                externalDto,
                competitionDriveId,
                entity,
                startFranchiseSeasonId,
                endFranchiseSeasonId);
        }
    }

    private async Task ProcessNewEntity(
        ProcessDocumentCommand command,
        EspnFootballEventCompetitionPlayDto externalDto,
        CompetitionBase competition,
        FootballCompetitionStatus? competitionStatus,
        Guid? competitionDriveId,
        Guid? startFranchiseSeasonId,
        Guid? endFranchiseSeasonId)
    {
        _logger.LogInformation("Creating new CompetitionPlay. CompetitionId={CompId}, DriveId={DriveId}, PlayType={PlayType}",
            competition.Id,
            competitionDriveId,
            externalDto.Type?.Text);

        var play = externalDto.AsFootballEntity(
            _externalRefIdentityGenerator,
            command.CorrelationId,
            competition.Id,
            competitionDriveId,
            startFranchiseSeasonId,
            endFranchiseSeasonId);

        // If the contest is underway, broadcast both the per-play log
        // event (sport-neutral) and the football scoreboard tick. The
        // tick carries the football shape (period, clock, possession,
        // scoring flag) consumed by the SignalR live-game UI.
        if (competitionStatus is not null && !competitionStatus.IsCompleted)
        {
            _logger.LogInformation(
                "Contest in progress, publishing ContestPlayCompleted + FootballContestStateChanged. ContestId={ContestId}, PlayId={PlayId}",
                competition.ContestId,
                play.Id);

            await _publishEndpoint.Publish(new ContestPlayCompleted(
                ContestId: competition.ContestId,
                CompetitionId: competition.Id,
                PlayId: play.Id,
                PlayDescription: play.Text,
                Ref: null,
                Sport: command.Sport,
                SeasonYear: command.SeasonYear,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionPlayDocumentProcessor));

            await _publishEndpoint.Publish(new FootballContestStateChanged(
                ContestId: competition.ContestId,
                Period: $"Q{play.PeriodNumber}",
                Clock: play.ClockDisplayValue ?? string.Empty,
                AwayScore: play.AwayScore,
                HomeScore: play.HomeScore,
                PossessionFranchiseSeasonId: play.StartFranchiseSeasonId,
                IsScoringPlay: play.ScoringPlay,
                Ref: null,
                Sport: command.Sport,
                SeasonYear: command.SeasonYear,
                CorrelationId: command.CorrelationId,
                CausationId: CausationId.Producer.EventCompetitionPlayDocumentProcessor));
        }

        await _dataContext.CompetitionPlays.AddAsync(play);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionPlay. CompetitionId={CompId}, PlayId={PlayId}, Sequence={Sequence}", 
            competition.Id,
            play.Id,
            play.SequenceNumber);
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnFootballEventCompetitionPlayDto externalDto,
        Guid? competitionDriveId,
        CompetitionPlayBase entity,
        Guid? startFranchiseSeasonId,
        Guid? endFranchiseSeasonId)
    {
        _logger.LogInformation("Updating CompetitionPlay. PlayId={PlayId}, DriveId={DriveId}",
            entity.Id,
            competitionDriveId);

        if (entity is not FootballCompetitionPlay footballPlay)
        {
            throw new InvalidOperationException(
                $"Expected FootballCompetitionPlay but got {entity.GetType().Name}. PlayId={entity.Id}");
        }

        footballPlay.StartFranchiseSeasonId = startFranchiseSeasonId;
        footballPlay.EndFranchiseSeasonId = endFranchiseSeasonId;
        footballPlay.DriveId = competitionDriveId;

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionPlay update. PlayId={PlayId}", entity.Id);
    }
}