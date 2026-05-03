using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Baseball;
using SportsData.Core.Infrastructure.Refs;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Baseball.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Baseball;

/// <summary>
/// MLB-specific processor for EventCompetitionStatus documents.
///
/// Why a separate processor: ESPN's MLB status payload includes
/// baseball-only fields (<c>halfInning</c>, <c>periodPrefix</c>) and a
/// <c>featuredAthletes</c> child collection (winning/losing pitcher
/// post-game; current batter/pitcher in-game) that the generic
/// <see cref="Common.EventCompetitionStatusDocumentProcessor{TDataContext}"/>
/// would silently drop because it deserializes to the base DTO and
/// constructs a base <c>CompetitionStatus</c>. This processor
/// deserializes to <see cref="EspnBaseballEventCompetitionStatusDto"/>
/// and constructs <see cref="BaseballCompetitionStatus"/> — the
/// sport-specific TPH subclass — so the MLB fields and the
/// FeaturedAthletes children persist alongside the shared status row.
/// </summary>
[DocumentProcessor(SourceDataProvider.Espn, Sport.BaseballMlb, DocumentType.EventCompetitionStatus)]
public class BaseballEventCompetitionStatusDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly IDateTimeProvider _dateTimeProvider;

    public BaseballEventCompetitionStatusDocumentProcessor(
        ILogger<BaseballEventCompetitionStatusDocumentProcessor<TDataContext>> logger,
        TDataContext dataContext,
        IEventBus publishEndpoint,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        IGenerateResourceRefs refGenerator,
        IDateTimeProvider dateTimeProvider)
        : base(logger, dataContext, publishEndpoint, externalRefIdentityGenerator, refGenerator)
    {
        _dateTimeProvider = dateTimeProvider;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var publishEvent = false;

        var dto = command.Document.FromJson<EspnBaseballEventCompetitionStatusDto>();

        if (dto is null)
        {
            _logger.LogError("Failed to deserialize EspnBaseballEventCompetitionStatusDto.");
            return; // terminal failure — don't retry
        }

        if (string.IsNullOrEmpty(dto.Ref?.ToString()))
        {
            _logger.LogError("EspnBaseballEventCompetitionStatusDto Ref is null or empty.");
            return; // terminal failure — don't retry
        }

        // Type / Type.Name are dereferenced unconditionally below in the
        // existing-status comparison and several log lines. The base
        // mapping is null-safe via `dto.Type?.Name ?? string.Empty`, but
        // this method's own diff path would NRE on a malformed payload.
        // Bail with a useful log instead.
        if (dto.Type is null || string.IsNullOrWhiteSpace(dto.Type.Name))
        {
            _logger.LogError(
                "EspnBaseballEventCompetitionStatusDto Type is missing or has empty Name. Ref={Ref}",
                dto.Ref);
            return; // terminal failure — don't retry
        }

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionStatusRefToCompetitionRef);

        if (competitionId is null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        var competitionIdValue = competitionId.Value;

        var entity = dto.AsEntity(
            _externalRefIdentityGenerator,
            competitionIdValue,
            command.CorrelationId,
            _dateTimeProvider.UtcNow());

        // Includes are scoped to the MLB subclass DbSet so we can pull
        // FeaturedAthletes (subclass-only nav) without an OfType cast.
        // The Competition nav was lifted off CompetitionStatus when the
        // Status relationship moved to the sport-specific competition
        // subclass — the FK is still on this side, but the inverse is
        // owned by BaseballCompetition.
        var existing = await _dataContext.Set<BaseballCompetitionStatus>()
            .Include(x => x.ExternalIds)
            .Include(x => x.FeaturedAthletes)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.CompetitionId == competitionIdValue);

        if (existing is not null)
        {
            publishEvent = existing.StatusTypeName != dto.Type.Name;

            _logger.LogInformation(
                "Updating MLB CompetitionStatus (hard replace). CompetitionId={CompId}, OldStatus={OldStatus}, NewStatus={NewStatus}, FeaturedAthleteCount={FeaturedAthleteCount}",
                competitionIdValue,
                existing.StatusTypeName,
                dto.Type.Name,
                entity.FeaturedAthletes.Count);

            // ExternalIds and FeaturedAthletes cascade-delete with the
            // parent (DeleteBehavior.Cascade configured on both child
            // configurations), so removing the parent is sufficient.
            _dataContext.Set<BaseballCompetitionStatus>().Remove(existing);
        }
        else
        {
            _logger.LogInformation(
                "Creating new MLB CompetitionStatus. CompetitionId={CompId}, Status={Status}, FeaturedAthleteCount={FeaturedAthleteCount}",
                competitionIdValue,
                dto.Type.Name,
                entity.FeaturedAthletes.Count);
        }

        if (publishEvent)
        {
            // ContestId is what crosses the service boundary — Competition
            // is a Producer-internal sub-aggregate. Projected read so we
            // don't pull the full Competition row.
            var contestId = await _dataContext.Competitions
                .Where(c => c.Id == competitionIdValue)
                .Select(c => c.ContestId)
                .FirstAsync();

            _logger.LogInformation(
                "MLB Contest status changed, publishing event. ContestId={ContestId}, CompetitionId={CompId}, NewStatus={Status}",
                contestId,
                competitionIdValue,
                entity.StatusTypeName);

            await _publishEndpoint.Publish(new ContestStatusChanged(
                contestId,
                entity.StatusTypeName,
                _refGenerator.ForCompetition(competitionIdValue),
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                CausationId.Producer.EventCompetitionStatusDocumentProcessor
            ));
        }

        await _dataContext.Set<BaseballCompetitionStatus>().AddAsync(entity);
        await _dataContext.SaveChangesAsync();

        _logger.LogInformation(
            "Persisted MLB CompetitionStatus. CompetitionId={CompId}, Status={Status}, HalfInning={HalfInning}, PeriodPrefix={PeriodPrefix}",
            competitionIdValue,
            entity.StatusTypeName,
            entity.HalfInning,
            entity.PeriodPrefix);
    }
}
