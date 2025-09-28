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
        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = command.CorrelationId }))
        {
            _logger.LogInformation("Began with {@command}", command);
            try { await ProcessInternal(command); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
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
            _logger.LogError("Invalid EspnEventCompetitionOddsDto or missing $ref. {@Command}", command);
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("Invalid ParentId format for CompetitionId.");
            return;
        }

        if (!command.Season.HasValue)
        {
            _logger.LogError("Command must have a SeasonYear defined");
            return;
        }

        var competition = await _db.Competitions
            .Include(x => x.Contest)
            .FirstOrDefaultAsync(x => x.Id == competitionId);

        if (competition is null)
            throw new ArgumentException("competition not found");

        // --- Identity + hash ---
        var identity = _idGen.Generate(dto.Ref); // stable odds id from $ref
        var contentHash = _jsonHash.NormalizeAndHash(command.Document);

        // Prefer canonical id lookup; fallback to ExternalIds (back-compat)
        var existing = await _db.CompetitionOdds
            .Include(o => o.ExternalIds)
            .Include(o => o.Teams)
            .Include(o => o.Links)
            .FirstOrDefaultAsync(o => o.Id == identity.CanonicalId);

        if (existing is null)
        {
            existing = await _db.CompetitionOdds
                .Include(o => o.ExternalIds)
                .Include(o => o.Teams)
                .Include(o => o.Links)
                .FirstOrDefaultAsync(x =>
                    x.ExternalIds.Any(e => e.SourceUrlHash == command.UrlHash &&
                                           e.Provider == command.SourceDataProvider));
        }

        // If nothing changed, bail out
        if (existing is not null && string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal))
        {
            _logger.LogInformation("No odds changes detected. Skip. comp={CompId} provider={Prov}",
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
            // Remove children first if you don’t 100% trust cascade in all environments
            if (existing.Teams?.Count > 0) _db.CompetitionTeamOdds.RemoveRange(existing.Teams);
            if (existing.Links?.Count > 0) _db.Set<CompetitionOddsLink>().RemoveRange(existing.Links);
            if (existing.ExternalIds?.Count > 0) _db.Set<CompetitionOddsExternalId>().RemoveRange(existing.ExternalIds);

            _db.CompetitionOdds.Remove(existing);
            await _db.SaveChangesAsync(); // ensure delete completed (and frees key)
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
                competition.Contest.Id, command.CorrelationId, CausationId.Producer.EventDocumentProcessor));
        }

        _logger.LogInformation("Odds upserted (hard replace). comp={CompId} provider={Prov} oddsId={OddsId}",
            competition.Id, dto.Provider.Id, incoming.Id);
    }
}
