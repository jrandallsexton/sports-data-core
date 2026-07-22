using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Exceptions;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionSituation)]
public class BaseballEventCompetitionSituationDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public BaseballEventCompetitionSituationDocumentProcessor(
        ILogger<BaseballEventCompetitionSituationDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refs,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refs)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var dto = command.Document.FromJson<EspnBaseballEventCompetitionSituationDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnBaseballEventCompetitionSituationDto.");
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
                await PublishDependencyRequest(
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

        // Resolve baserunner occupancy (onFirst/onSecond/onThird). The refs are
        // season-scoped athlete URLs → AthleteSeason. When a baserunner isn't
        // sourced yet, request it and throw so the base retries after it lands
        // (same dependency-sourcing pattern used above for LastPlay). All missing
        // baserunners are requested before a single throw.
        var (onFirstId, missingFirst) = await ResolveBaserunnerAsync(command, dto.OnFirst);
        var (onSecondId, missingSecond) = await ResolveBaserunnerAsync(command, dto.OnSecond);
        var (onThirdId, missingThird) = await ResolveBaserunnerAsync(command, dto.OnThird);

        if (missingFirst || missingSecond || missingThird)
        {
            throw new ExternalDocumentNotSourcedException(
                "One or more baserunners reference an AthleteSeason that isn't sourced yet. " +
                "Requested the missing dependencies; will retry this situation.");
        }

        var entity = new BaseballCompetitionSituation
        {
            Id = identity.CanonicalId,
            CompetitionId = competitionIdValue,
            LastPlayId = lastPlayId,
            Balls = dto.Balls,
            Strikes = dto.Strikes,
            Outs = dto.Outs,
            OnFirstAthleteSeasonId = onFirstId,
            OnSecondAthleteSeasonId = onSecondId,
            OnThirdAthleteSeasonId = onThirdId,
            CreatedBy = command.CorrelationId,
            CreatedUtc = _dateTimeProvider.UtcNow()
        };

        foreach (var note in dto.SituationNotes ?? Enumerable.Empty<EspnBaseballSituationNoteDto>())
        {
            entity.Notes.Add(new BaseballCompetitionSituationNote
            {
                Id = Guid.NewGuid(),
                SituationId = entity.Id,
                Type = note.Type,
                Text = note.Text,
                CreatedBy = command.CorrelationId,
                CreatedUtc = _dateTimeProvider.UtcNow()
            });
        }

        await _dataContext.Set<BaseballCompetitionSituation>().AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted baseball CompetitionSituation. CompetitionId={CompId}, SituationId={SituationId}, " +
            "Balls={Balls}, Strikes={Strikes}, Outs={Outs}, Notes={NoteCount}",
            competitionIdValue, entity.Id, entity.Balls, entity.Strikes, entity.Outs, entity.Notes.Count);
    }

    /// <summary>
    /// Resolve a baserunner's season-scoped athlete ref to an AthleteSeason id.
    /// Returns (null, false) when the base is empty (no ref). When the ref is
    /// present but unresolved, request the AthleteSeason and return
    /// (null, true) so the caller can throw once after requesting all missing
    /// baserunners.
    /// </summary>
    private async Task<(Guid? Id, bool Missing)> ResolveBaserunnerAsync(
        ProcessDocumentCommand command,
        EspnBaseballSituationPlayerDto? runner)
    {
        if (runner?.Athlete?.Ref is null)
            return (null, false);

        var athleteSeasonId = await _dataContext.ResolveIdAsync<AthleteSeason, AthleteSeasonExternalId>(
            runner.Athlete,
            command.SourceDataProvider,
            () => _dataContext.AthleteSeasons,
            externalIdsNav: "ExternalIds",
            key: a => a.Id);

        if (athleteSeasonId is null)
        {
            await PublishDependencyRequest<Guid?>(
                command, runner.Athlete, parentId: null, DocumentType.AthleteSeason);
            return (null, true);
        }

        return (athleteSeasonId, false);
    }
}
