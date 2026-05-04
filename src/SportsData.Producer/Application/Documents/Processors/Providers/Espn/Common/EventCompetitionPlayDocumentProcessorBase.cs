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

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

/// <summary>
/// Shared base for sport-specific EventCompetitionPlay processors. Owns the
/// orchestration that's identical across sports — DTO validation, parent
/// derivation, competition load, identity lookup, the new-vs-update branch,
/// and the sport-neutral <see cref="ContestPlayCompleted"/> publish gate.
/// Sport-specific bits (status loading, entity creation, scoreboard tick,
/// update mutation) are delegated to abstract hooks.
/// </summary>
public abstract class EventCompetitionPlayDocumentProcessorBase<TDataContext, TDto>
    : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
    where TDto : EspnEventCompetitionPlayDtoBase
{
    protected EventCompetitionPlayDocumentProcessorBase(
        ILogger logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
    }

    protected sealed override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<TDto>();

        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize {DtoType}.", typeof(TDto).Name);
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("{DtoType} Ref is null.", typeof(TDto).Name);
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
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == competitionIdValue);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionIdValue);
            throw new InvalidOperationException($"Competition with ID {competitionIdValue} does not exist.");
        }

        // Tri-state: null means the sport-specific status row hasn't
        // been sourced yet — treat as transient (status processor races
        // play processor on first sourcing) and let MassTransit retry
        // rather than silently skipping the live broadcast.
        var inProgress = await IsCompetitionInProgressAsync(competitionIdValue);
        if (inProgress is null)
        {
            _logger.LogWarning(
                "Competition status not yet sourced for CompetitionId={CompetitionId}; throwing for retry.",
                competitionIdValue);
            throw new InvalidOperationException(
                $"Competition status not yet sourced for CompetitionId={competitionIdValue}; cannot determine in-progress state for play broadcast.");
        }

        var playIdentity = _externalRefIdentityGenerator.Generate(externalDto.Ref);

        var entity = await _dataContext.CompetitionPlays
            .Include(x => x.ExternalIds)
            .FirstOrDefaultAsync(x => x.Id == playIdentity.CanonicalId);

        if (entity is null)
        {
            _logger.LogInformation("Processing new CompetitionPlayBase entity. Ref={Ref}", externalDto.Ref);

            var play = await BuildNewPlayAsync(command, externalDto, competition);

            if (inProgress.Value)
            {
                _logger.LogInformation(
                    "Contest in progress, publishing ContestPlayCompleted. ContestId={ContestId}, PlayId={PlayId}",
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

                await PublishSportSpecificStateAsync(command, competition, play);
            }

            await _dataContext.CompetitionPlays.AddAsync(play);
            await _dataContext.SaveChangesAsync();

            _logger.LogInformation(
                "Persisted CompetitionPlay. CompetitionId={CompId}, PlayId={PlayId}, Sequence={Sequence}",
                competition.Id, play.Id, play.SequenceNumber);
        }
        else
        {
            _logger.LogInformation("Processing CompetitionPlay update. PlayId={PlayId}, Ref={Ref}", entity.Id, externalDto.Ref);

            await ApplyUpdateAsync(entity, command, externalDto);

            await _dataContext.SaveChangesAsync();

            _logger.LogInformation("Persisted CompetitionPlay update. PlayId={PlayId}", entity.Id);
        }
    }

    /// <summary>
    /// Returns true when the competition's status indicates it is mid-game,
    /// false when the status row exists and is completed, or null when no
    /// status row exists yet (status processor hasn't caught up to this
    /// play). The base treats null as transient and throws so MassTransit
    /// retries — preferable to silently swallowing the live broadcast.
    /// Subclasses load the sport-specific *CompetitionStatus subtype.
    /// </summary>
    protected abstract Task<bool?> IsCompetitionInProgressAsync(Guid competitionId);

    /// <summary>
    /// Materialize the sport-specific play entity from the wire DTO. Implementations
    /// resolve team/franchise refs, drive ids, etc., and call the appropriate
    /// AsXxxEntity extension.
    /// </summary>
    protected abstract Task<CompetitionPlayBase> BuildNewPlayAsync(
        ProcessDocumentCommand command,
        TDto externalDto,
        CompetitionBase competition);

    /// <summary>
    /// Publish the sport-specific scoreboard tick (e.g. FootballContestStateChanged).
    /// Default is a no-op for sports that don't yet have a tick event. Only invoked
    /// when the contest is in progress.
    /// </summary>
    protected virtual Task PublishSportSpecificStateAsync(
        ProcessDocumentCommand command,
        CompetitionBase competition,
        CompetitionPlayBase play) => Task.CompletedTask;

    /// <summary>
    /// Apply incoming DTO state to an existing entity. Subclasses cast to their
    /// concrete play type and set the fields they own.
    /// </summary>
    protected abstract Task ApplyUpdateAsync(
        CompetitionPlayBase entity,
        ProcessDocumentCommand command,
        TDto externalDto);
}
