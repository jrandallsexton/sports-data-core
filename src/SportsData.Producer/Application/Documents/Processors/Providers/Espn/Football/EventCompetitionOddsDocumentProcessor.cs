using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

using SportsData.Core.Infrastructure.Refs;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionOdds)]
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

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("Invalid ParentId format for CompetitionId. ParentId={ParentId}", command.ParentId);
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command missing SeasonYear.");
            return;
        }

        var competition = await _dataContext.Competitions
            .AsNoTracking()
            .Include(x => x.Contest)
            .FirstOrDefaultAsync(x => x.Id == competitionId);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionId);
            throw new ArgumentException("competition not found");
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
        await _dataContext.SaveChangesAsync(); // Atomic: both delete + insert in single transaction
        
        // Publish after success
        if (existing is null)
        {
            await _publishEndpoint.Publish(new ContestOddsCreated(
                competition.Contest.Id,
                null,
                command.Sport,
                command.Season,
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
                command.Season,
                command.CorrelationId,
                CausationId.Producer.EventDocumentProcessor));
        }

        _logger.LogInformation("Persisted CompetitionOdds. CompetitionId={CompId}, Provider={Prov}, OddsId={OddsId}",
            competition.Id, dto.Provider.Id, incoming.Id);
    }
}
