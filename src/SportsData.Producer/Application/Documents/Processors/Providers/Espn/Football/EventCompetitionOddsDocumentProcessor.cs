using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Eventing;
using SportsData.Core.Eventing.Events.Contests;
using SportsData.Core.Extensions;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;
using SportsData.Producer.Application.Documents.Processors.Commands;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Application.Documents.Processors.Providers.Espn.Football;

[DocumentProcessor(SourceDataProvider.Espn, Sport.FootballNcaa, DocumentType.EventCompetitionOdds)]
public class EventCompetitionOddsDocumentProcessor<TDataContext> : IProcessDocuments
    where TDataContext : TeamSportDataContext
{
    private readonly ILogger<EventCompetitionOddsDocumentProcessor<TDataContext>> _logger;
    private readonly TDataContext _db;
    private readonly IEventBus _bus;
    private readonly IGenerateExternalRefIdentities _idGen;
    private readonly IJsonHashCalculator _jsonHash;

    public EventCompetitionOddsDocumentProcessor(
        ILogger<EventCompetitionOddsDocumentProcessor<TDataContext>> logger,
        TDataContext db,
        IEventBus bus,
        IGenerateExternalRefIdentities idGen,
        IJsonHashCalculator jsonHash)
    {
        _logger = logger;
        _db = db;
        _bus = bus;
        _idGen = idGen;
        _jsonHash = jsonHash;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId,
                   ["DocumentType"] = command.DocumentType,
                   ["Season"] = command.Season ?? 0,
                   ["CompetitionId"] = command.ParentId ?? "Unknown"
               }))
        {
            _logger.LogInformation("EventCompetitionOddsDocumentProcessor started. {@Command}", command);

            try
            {
                await ProcessInternal(command);
                
                _logger.LogInformation("EventCompetitionOddsDocumentProcessor completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "EventCompetitionOddsDocumentProcessor failed.");
                throw;
            }
        }
    }

    private async Task ProcessInternal(ProcessDocumentCommand command)
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

        var competition = await _db.Competitions
            .AsNoTracking()
            .Include(x => x.Contest)
            .FirstOrDefaultAsync(x => x.Id == competitionId);

        if (competition is null)
        {
            _logger.LogError("Competition not found. CompetitionId={CompetitionId}", competitionId);
            throw new ArgumentException("competition not found");
        }

        // --- Identity + hash ---
        var identity = _idGen.Generate(dto.Ref); // stable odds id from $ref
        var contentHash = _jsonHash.NormalizeAndHash(command.Document);

        // Prefer canonical id lookup; fallback to ExternalIds (back-compat)
        var existing = await _db.CompetitionOdds
            .Include(o => o.ExternalIds)
            .Include(o => o.Teams)
            .Include(o => o.Links)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == identity.CanonicalId);

        if (existing is null)
        {
            existing = await _db.CompetitionOdds
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
            externalRefIdentityGenerator: _idGen,
            competitionId: competition.Id,
            homeFranchiseSeasonId: competition.Contest.HomeTeamFranchiseSeasonId,
            awayFranchiseSeasonId: competition.Contest.AwayTeamFranchiseSeasonId,
            correlationId: command.CorrelationId,
            contentHash: contentHash);

        // --- HARD REPLACE (no transactions; EF InMemory friendly) ---
        if (existing is not null)
        {
            _logger.LogInformation("Updating CompetitionOdds (hard replace). CompetitionId={CompId}, OddsId={OddsId}", 
                competition.Id, 
                existing.Id);

            // Remove children first if you don't 100% trust cascade in all environments
            if (existing.Teams?.Count > 0) _db.CompetitionTeamOdds.RemoveRange(existing.Teams);
            if (existing.Links?.Count > 0) _db.Set<CompetitionOddsLink>().RemoveRange(existing.Links);
            if (existing.ExternalIds?.Count > 0) _db.Set<CompetitionOddsExternalId>().RemoveRange(existing.ExternalIds);

            _db.CompetitionOdds.Remove(existing);
            await _db.SaveChangesAsync(); // ensure delete completed (and frees key)
        }
        else
        {
            _logger.LogInformation("Creating new CompetitionOdds. CompetitionId={CompId}", competition.Id);
        }

        await _db.CompetitionOdds.AddAsync(incoming);
        await _db.SaveChangesAsync();
        
        // Publish after success
        if (existing is null)
        {
            await _bus.Publish(new ContestOddsCreated(
                competition.Contest.Id, command.CorrelationId, CausationId.Producer.EventDocumentProcessor));
        }
        else
        {
            await _bus.Publish(new ContestOddsUpdated(
                competition.Contest.Id, "ContestOddsUpdated", command.CorrelationId, CausationId.Producer.EventDocumentProcessor));
        }

        _logger.LogInformation("Persisted CompetitionOdds. CompetitionId={CompId}, Provider={Prov}, OddsId={OddsId}",
            competition.Id, dto.Provider.Id, incoming.Id);
    }
}
