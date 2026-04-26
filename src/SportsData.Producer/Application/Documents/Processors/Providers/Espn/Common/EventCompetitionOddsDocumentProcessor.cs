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
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Common;

// MLB intentionally absent here — its odds wire shape is a paged wrapper
// that doesn't carry per-item $refs, so it's handled by
// BaseballEventCompetitionOddsDocumentProcessor (sport-specific processor
// under Application/Documents/Processors/Providers/Espn/Baseball/).
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionOdds)]
[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNfl, DocumentType.EventCompetitionOdds)]
public class EventCompetitionOddsDocumentProcessor<TDataContext> : DocumentProcessorBase<TDataContext>
    where TDataContext : TeamSportDataContext
{
    private readonly IJsonHashCalculator _jsonHash;

    public EventCompetitionOddsDocumentProcessor(
        ILogger<EventCompetitionOddsDocumentProcessor<TDataContext>> logger,
        TDataContext db,
        IEventBus bus,
        IGenerateExternalRefIdentities idGen,
        IGenerateResourceRefs refs,
        IJsonHashCalculator jsonHash)
        : base(logger, db, bus, idGen, refs)
    {
        _jsonHash = jsonHash;
    }

    protected override async Task ProcessInternal(ProcessDocumentCommand command)
    {
        // --- Validate inputs ---
        var dto = command.Document.FromJson<EspnEventCompetitionOddsDto>();
        if (dto is null || dto.Ref is null)
        {
            _logger.LogError("Invalid EspnEventCompetitionOddsDto or missing $ref.");
            return;
        }

        var competitionId = TryGetOrDeriveParentId(
            command,
            EspnUriMapper.CompetitionOddsRefToCompetitionRef);

        // Contract violation, not a transient issue — the URI doesn't map to
        // a Competition we can derive. Retrying won't help. Log + return so
        // the message is acked and the DLQ stays clean.
        if (competitionId == null)
        {
            _logger.LogError("Unable to determine CompetitionId from ParentId or URI");
            return;
        }

        var competitionIdValue = competitionId.Value;

        // Same — message envelope is missing a required field. Retrying the
        // same malformed message produces the same result.
        if (!command.SeasonYear.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            return;
        }

        var competition = await _dataContext.Competitions
            .AsNoTracking()
            .Include(x => x.Contest)
            .FirstOrDefaultAsync(x => x.Id == competitionIdValue);

        // Transient — the parent Competition document is likely still
        // being processed upstream. Throw so Hangfire's AutomaticRetryAttribute
        // re-runs us with backoff (eventual consistency).
        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionIdValue);
            throw new InvalidOperationException(
                $"Competition {competitionIdValue} not found; will retry.");
        }

        // --- Identity + hash ---
        var identity = _externalRefIdentityGenerator.Generate(dto.Ref); // stable odds id from $ref
        var contentHash = _jsonHash.NormalizeAndHash(command.Document);

        // Prefer canonical id lookup; fallback to ExternalIds (back-compat)
        var existing = await _dataContext.CompetitionOdds
            .Include(o => o.ExternalIds)
            .Include(o => o.Teams)
            .Include(o => o.Links)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == identity.CanonicalId);

        if (existing is null)
        {
            existing = await _dataContext.CompetitionOdds
                .Include(o => o.Teams)
                .Include(o => o.Links)
                .Include(competitionOdds => competitionOdds.ExternalIds)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x =>
                    x.ExternalIds.Any(e => e.SourceUrlHash == command.UrlHash &&
                                           e.Provider == command.SourceDataProvider));
        }

        // If nothing changed, bail out
        if (existing is not null && string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal))
        {
            _logger.LogInformation("No odds changes detected, skipping. CompetitionId={CompId}, Provider={Prov}",
                competition.Id, dto.Provider.Id);
            return;
        }

        // Defensive guard — Competition.Contest is loaded via .Include above
        // and the FK should be non-nullable, but a missing parent would NRE
        // on the franchise-season accessors below. Throw with context so the
        // operator can investigate (data integrity issue, or a race where
        // the Contest hasn't been persisted yet — Hangfire's retry policy
        // will pick it up).
        if (competition.Contest is null)
        {
            _logger.LogError(
                "Competition.Contest is null; cannot resolve franchise-season ids. CompetitionId={CompId}, CorrelationId={CorrelationId}",
                competition.Id, command.CorrelationId);
            throw new InvalidOperationException(
                $"Competition {competition.Id} has no Contest loaded; cannot persist odds. CorrelationId={command.CorrelationId}");
        }

        // Build the authoritative incoming graph
        var incoming = dto.AsEntity(
            externalRefIdentityGenerator: _externalRefIdentityGenerator,
            competitionId: competition.Id,
            homeFranchiseSeasonId: competition.Contest.HomeTeamFranchiseSeasonId,
            awayFranchiseSeasonId: competition.Contest.AwayTeamFranchiseSeasonId,
            correlationId: command.CorrelationId,
            contentHash: contentHash);

        // Remove existing odds if present (EF Core cascade delete handles children)
        if (existing is not null)
        {
            _logger.LogInformation("Updating CompetitionOdds (hard replace). CompetitionId={CompId}, OddsId={OddsId}", 
                competition.Id, 
                existing.Id);

            _dataContext.CompetitionOdds.Remove(existing);
            // Cascade delete configured in entity configuration removes Teams, Links, and ExternalIds automatically
        }
        else
        {
            _logger.LogInformation("Creating new CompetitionOdds. CompetitionId={CompId}", competition.Id);
        }

        await _dataContext.CompetitionOdds.AddAsync(incoming);

        if (existing is null)
        {
            await _publishEndpoint.Publish(new ContestOddsCreated(
                competition.Contest.Id,
                null,
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                command.MessageId));
        }
        else
        {
            await _publishEndpoint.Publish(new ContestOddsUpdated(
                competition.Contest.Id,
                "ContestOddsUpdated",
                null,
                command.Sport,
                command.SeasonYear,
                command.CorrelationId,
                CausationId.Producer.EventDocumentProcessor));
        }

        await _dataContext.SaveChangesAsync();

        _logger.LogInformation("Persisted CompetitionOdds. CompetitionId={CompId}, Provider={Prov}, OddsId={OddsId}",
            competition.Id, dto.Provider.Id, incoming.Id);
    }
}
