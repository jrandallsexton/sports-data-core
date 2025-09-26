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
    private readonly IJsonHashCalculator _jsonHashCalculator;

    public EventCompetitionOddsDocumentProcessor(
        ILogger<EventCompetitionOddsDocumentProcessor<TDataContext>> logger,
        TDataContext db,
        IEventBus bus,
        IGenerateExternalRefIdentities idGen,
        IJsonHashCalculator jsonHashCalculator)
    {
        _logger = logger;
        _db = db;
        _bus = bus;
        _idGen = idGen;
        _jsonHashCalculator = jsonHashCalculator;
    }

    public async Task ProcessAsync(ProcessDocumentCommand command)
    {
        using (_logger.BeginScope(new Dictionary<string, object>
               {
                   ["CorrelationId"] = command.CorrelationId
               }))
        {
            _logger.LogInformation("Began with {@command}", command);

            try
            {
                await ProcessInternal(command);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing. {@Command}", command);
                throw;
            }
        }
    }
    private async Task ProcessInternal(ProcessDocumentCommand command)
    {
        var externalDto = command.Document.FromJson<EspnEventCompetitionOddsDto>();
        if (externalDto is null)
        {
            _logger.LogError("Failed to deserialize document to EspnEventCompetitionDto. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(externalDto.Ref?.ToString()))
        {
            _logger.LogError("EspnEventCompetitionDto Ref is null. {@Command}", command);
            return;
        }

        if (string.IsNullOrEmpty(command.ParentId))
        {
            _logger.LogError("ParentId not provided. Cannot process competition for null CompetitionId");
            return;
        }

        if (!Guid.TryParse(command.ParentId, out var competitionId))
        {
            _logger.LogError("Invalid ParentId format for CompetitionId. Cannot parse to Guid.");
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
        {
            _logger.LogError("Competition not found");
            throw new ArgumentException("competition not found");
        }

        // NOTE: include Links for replacement on change
        var existing = await _db.CompetitionOdds
            .Include(x => x.ExternalIds)
            .Include(x => x.Teams).ThenInclude(t => t.Snapshots)
            .Include(x => x.Totals)
            .Include(x => x.Links)
            .FirstOrDefaultAsync(x =>
                x.ExternalIds.Any(e => e.SourceUrlHash == command.UrlHash &&
                                     e.Provider == command.SourceDataProvider));

        var contentHash = _jsonHashCalculator.NormalizeAndHash(command.Document);

        if (existing is null)
        {
            await ProcessNew(command, externalDto, competition.Id, competition.Contest, contentHash);
            return;
        }

        // SHORT-CIRCUIT: content didn't change → skip work
        if (string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal))
        {
            _logger.LogInformation("No changes detected for odds. Skipping update. {@CompetitionId} {@Provider?}", competition.Id, existing.ProviderId);
            return;
        }

        await ProcessUpdate(command, externalDto, existing, competition.Id, competition.Contest, contentHash);
    }

    private async Task ProcessNew(
        ProcessDocumentCommand command,
        EspnEventCompetitionOddsDto dto,
        Guid competitionId,
        Contest contest,
        string contentHash)
    {
        var entity = dto.AsEntity(
            externalRefIdentityGenerator: _idGen,
            competitionId: competitionId,
            homeFranchiseSeasonId: contest.HomeTeamFranchiseSeasonId,
            awayFranchiseSeasonId: contest.AwayTeamFranchiseSeasonId,
            correlationId: command.CorrelationId,
            contentHash: contentHash);

        await _db.CompetitionOdds.AddAsync(entity);

        await _bus.Publish(new ContestOddsCreated(
            contest.Id, command.CorrelationId, CausationId.Producer.EventDocumentProcessor));

        await _db.SaveChangesAsync();
    }

    private async Task ProcessUpdate(
        ProcessDocumentCommand command,
        EspnEventCompetitionOddsDto dto,
        CompetitionOdds existing,
        Guid competitionId,
        Contest contest,
        string contentHash)
    {
        // Build an incoming graph with the same mapper as create
        var incoming = dto.AsEntity(
            externalRefIdentityGenerator: _idGen,
            competitionId: competitionId,
            homeFranchiseSeasonId: contest.HomeTeamFranchiseSeasonId,
            awayFranchiseSeasonId: contest.AwayTeamFranchiseSeasonId,
            correlationId: command.CorrelationId,
            contentHash: contentHash);

        // Merge (no delete/recreate)
        MergeCompetitionOdds(existing, incoming);

        // Ensure ExternalIds contains the (UrlHash, Provider) row
        if (!existing.ExternalIds.Any(e => e.SourceUrlHash == command.UrlHash &&
                                           e.Provider == command.SourceDataProvider))
        {
            existing.ExternalIds.Add(new CompetitionOddsExternalId
            {
                // Id seeded by your base class constructor
                Provider = command.SourceDataProvider,
                SourceUrl = dto.Ref.OriginalString,
                SourceUrlHash = command.UrlHash,
                Value = command.UrlHash
            });
        }

        await _bus.Publish(new ContestOddsUpdated(
            contest.Id, command.CorrelationId, CausationId.Producer.EventDocumentProcessor));

        await _db.SaveChangesAsync();

        _logger.LogInformation("Competition odds updated. {@CompetitionId} Provider:{@ProviderId}",
            competitionId, existing.ProviderId);
    }

    private static void MergeCompetitionOdds(CompetitionOdds existing, CompetitionOdds incoming)
    {
        // Root fields
        existing.ProviderRef = incoming.ProviderRef;
        existing.ProviderName = incoming.ProviderName;
        existing.ProviderPriority = incoming.ProviderPriority;

        existing.Details = incoming.Details;
        existing.OverUnder = incoming.OverUnder;
        existing.Spread = incoming.Spread;
        existing.OverOdds = incoming.OverOdds;
        existing.UnderOdds = incoming.UnderOdds;
        existing.MoneylineWinner = incoming.MoneylineWinner;
        existing.SpreadWinner = incoming.SpreadWinner;
        existing.PropBetsRef = incoming.PropBetsRef;
        existing.ContentHash = incoming.ContentHash;

        // -----------------------------
        // Links: diff by (Rel, Href)
        // -----------------------------
        static string LinkKey(string? rel, string? href)
            => $"{rel ?? string.Empty}\u001F{href ?? string.Empty}";

        var incomingLinkMap = incoming.Links
            .ToDictionary(l => LinkKey(l.Rel, l.Href), l => l, StringComparer.Ordinal);

        // remove links that are no longer present
        foreach (var ex in existing.Links.ToList())
        {
            var key = LinkKey(ex.Rel, ex.Href);
            if (!incomingLinkMap.ContainsKey(key))
                existing.Links.Remove(ex);
        }

        // add or update links
        foreach (var inc in incoming.Links)
        {
            var key = LinkKey(inc.Rel, inc.Href);
            var match = existing.Links.FirstOrDefault(l => LinkKey(l.Rel, l.Href) == key);
            if (match is null)
            {
                existing.Links.Add(new CompetitionOddsLink
                {
                    // Id auto-seeded by base class
                    Rel = inc.Rel,
                    Language = inc.Language,
                    Href = inc.Href,
                    Text = inc.Text,
                    ShortText = inc.ShortText,
                    IsExternal = inc.IsExternal,
                    IsPremium = inc.IsPremium
                });
            }
            else
            {
                match.Language = inc.Language;
                match.Text = inc.Text;
                match.ShortText = inc.ShortText;
                match.IsExternal = inc.IsExternal;
                match.IsPremium = inc.IsPremium;
            }
        }

        // --------------------------------
        // Teams: keyed by Side (Home/Away)
        // --------------------------------
        foreach (var incTeam in incoming.Teams)
        {
            var exTeam = existing.Teams.FirstOrDefault(t => t.Side == incTeam.Side);
            if (exTeam is null)
            {
                exTeam = new CompetitionTeamOdds
                {
                    // Id auto-seeded
                    Side = incTeam.Side
                    // FranchiseSeasonId set below
                };
                existing.Teams.Add(exTeam);
            }

            exTeam.IsFavorite = incTeam.IsFavorite;
            exTeam.IsUnderdog = incTeam.IsUnderdog;
            exTeam.HeadlineMoneyLine = incTeam.HeadlineMoneyLine;
            exTeam.HeadlineSpreadOdds = incTeam.HeadlineSpreadOdds;
            exTeam.FranchiseSeasonId = incTeam.FranchiseSeasonId;

            // Snapshots: upsert by Phase
            foreach (var incSnap in incTeam.Snapshots)
            {
                var exSnap = exTeam.Snapshots.FirstOrDefault(s => s.Phase == incSnap.Phase);
                if (exSnap is null)
                {
                    exSnap = new CompetitionTeamOddsSnapshot
                    {
                        // Id auto-seeded
                        Phase = incSnap.Phase
                    };
                    exTeam.Snapshots.Add(exSnap);
                }

                exSnap.IsFavorite = incSnap.IsFavorite;
                exSnap.IsUnderdog = incSnap.IsUnderdog;

                exSnap.PointSpreadRaw = incSnap.PointSpreadRaw;
                exSnap.PointSpreadNum = incSnap.PointSpreadNum;

                exSnap.SpreadValue = incSnap.SpreadValue;
                exSnap.SpreadDisplay = incSnap.SpreadDisplay;
                exSnap.SpreadAlt = incSnap.SpreadAlt;
                exSnap.SpreadDecimal = incSnap.SpreadDecimal;
                exSnap.SpreadFraction = incSnap.SpreadFraction;
                exSnap.SpreadAmerican = incSnap.SpreadAmerican;
                exSnap.SpreadOutcome = incSnap.SpreadOutcome;

                exSnap.MoneylineValue = incSnap.MoneylineValue;
                exSnap.MoneylineDisplay = incSnap.MoneylineDisplay;
                exSnap.MoneylineAlt = incSnap.MoneylineAlt;
                exSnap.MoneylineDecimal = incSnap.MoneylineDecimal;
                exSnap.MoneylineFraction = incSnap.MoneylineFraction;
                exSnap.MoneylineAmerican = incSnap.MoneylineAmerican;
                exSnap.MoneylineOutcome = incSnap.MoneylineOutcome;
                exSnap.MoneylineAmericanNum = incSnap.MoneylineAmericanNum;

                exSnap.SourceUrlHash = incSnap.SourceUrlHash; // if you populate it
            }

            // If you want to remove missing phases, do it explicitly:
            // foreach (var exPhase in exTeam.Snapshots.ToList())
            //     if (!incTeam.Snapshots.Any(s => s.Phase == exPhase.Phase))
            //         exTeam.Snapshots.Remove(exPhase);
        }

        // ----------------------------
        // Totals: upsert by Phase
        // ----------------------------
        foreach (var incTot in incoming.Totals)
        {
            var exTot = existing.Totals.FirstOrDefault(t => t.Phase == incTot.Phase);
            if (exTot is null)
            {
                exTot = new CompetitionTotalsSnapshot
                {
                    // Id auto-seeded
                    Phase = incTot.Phase
                };
                existing.Totals.Add(exTot);
            }

            exTot.OverValue = incTot.OverValue;
            exTot.OverDisplay = incTot.OverDisplay;
            exTot.OverAlt = incTot.OverAlt;
            exTot.OverDecimal = incTot.OverDecimal;
            exTot.OverFraction = incTot.OverFraction;
            exTot.OverAmerican = incTot.OverAmerican;
            exTot.OverOutcome = incTot.OverOutcome;

            exTot.UnderValue = incTot.UnderValue;
            exTot.UnderDisplay = incTot.UnderDisplay;
            exTot.UnderAlt = incTot.UnderAlt;
            exTot.UnderDecimal = incTot.UnderDecimal;
            exTot.UnderFraction = incTot.UnderFraction;
            exTot.UnderAmerican = incTot.UnderAmerican;
            exTot.UnderOutcome = incTot.UnderOutcome;

            // IMPORTANT: TotalValue is the parsed LINE (e.g., 54.5), not the price
            exTot.TotalValue = incTot.TotalValue;
            exTot.TotalDisplay = incTot.TotalDisplay;
            exTot.TotalAlt = incTot.TotalAlt;
            exTot.TotalDecimal = incTot.TotalDecimal;
            exTot.TotalFraction = incTot.TotalFraction;
            exTot.TotalAmerican = incTot.TotalAmerican;

            exTot.SourceUrlHash = incTot.SourceUrlHash; // if set in AsEntity()
        }

        // If you want to prune missing totals phases:
        // foreach (var ex in existing.Totals.ToList())
        //     if (!incoming.Totals.Any(t => t.Phase == ex.Phase))
        //         existing.Totals.Remove(ex);
    }
}