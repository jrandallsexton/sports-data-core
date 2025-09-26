using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

using System.Globalization;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions;

public static class CompetitionOddsExtensions
{
    public static CompetitionOdds AsEntity(
        this EspnEventCompetitionOddsDto src,
        IGenerateExternalRefIdentities externalRefIdentityGenerator,
        Guid competitionId,
        Guid homeFranchiseSeasonId,
        Guid awayFranchiseSeasonId,
        Guid correlationId,
        string? contentHash = null)
    {
        var identity = externalRefIdentityGenerator.Generate(src.Ref);

        var e = new CompetitionOdds
        {
            Id = identity.CanonicalId,   // stable per $ref (…/competitions/{id}/odds/{providerId})
            CompetitionId = competitionId,
            ProviderRef = src.Provider.Ref,
            ProviderId = src.Provider.Id,
            ProviderName = src.Provider.Name,
            ProviderPriority = src.Provider.Priority,

            Details = src.Details,

            // HEADLINES (current)
            OverUnder = (decimal?)src.OverUnder,
            Spread = (decimal?)src.Spread,
            OverOdds = (decimal?)src.OverOdds,   // American odds as decimal? matches your entity type
            UnderOdds = (decimal?)src.UnderOdds,

            MoneylineWinner = src.MoneylineWinner,
            SpreadWinner = src.SpreadWinner,

            // New bits
            PropBetsRef = src.PropBets?.Ref,
            ContentHash = contentHash,

            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,

            ExternalIds = new List<CompetitionOddsExternalId>
            {
                new()
                {
                    Id            = Guid.NewGuid(),
                    Value         = identity.UrlHash,
                    Provider      = SourceDataProvider.Espn,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl     = identity.CleanUrl
                }
            }
        };

        // Provider deeplinks
        if (src.Links is { Count: > 0 })
        {
            foreach (var l in src.Links)
            {
                e.Links.Add(new CompetitionOddsLink
                {
                    CompetitionOddsId = e.Id,
                    Rel               = l.Rel is { Count: > 0 } ? string.Join(',', l.Rel) : string.Empty,
                    Language          = l.Language,
                    Href              = l.Href.OriginalString,
                    Text              = l.Text,
                    ShortText         = l.ShortText,
                    IsExternal        = l.IsExternal,
                    IsPremium         = l.IsPremium
                });
            }
        }

        // Teams (Home / Away) - current headline fields live on CompetitionTeamOdds
        var home = BuildTeam(src.HomeTeamOdds, "Home", homeFranchiseSeasonId);
        var away = BuildTeam(src.AwayTeamOdds, "Away", awayFranchiseSeasonId);
        e.Teams.Add(home);
        e.Teams.Add(away);

        // Phase snapshots per side (Open/Close/Current)
        AddPhaseSnapshots(home, src.HomeTeamOdds, sourceUrlHash: identity.UrlHash, sideIsHome: true);
        AddPhaseSnapshots(away, src.AwayTeamOdds, sourceUrlHash: identity.UrlHash, sideIsHome: false);

        // Totals snapshots (Open/Close/Current) — parse TOTAL LINE from total.american/alt (not .value)
        AddTotalsSnapshot(e, "Open", src.Open, identity.UrlHash);
        AddTotalsSnapshot(e, "Close", src.Close, identity.UrlHash);
        AddTotalsSnapshot(e, "Current", src.Current, identity.UrlHash);

        return e;
    }

    private static CompetitionTeamOdds BuildTeam(
        EspnEventCompetitionOddsTeamOdds t,
        string side,
        Guid franchiseSeasonId)
    {
        return new CompetitionTeamOdds
        {
            Side               = side,
            IsFavorite         = t.Favorite,                 // headline (current)
            IsUnderdog         = t.Underdog,                 // headline (current)
            HeadlineMoneyLine  = t.MoneyLine,        // current ML
            HeadlineSpreadOdds = (decimal?)t.SpreadOdds, // current spread price
            FranchiseSeasonId  = franchiseSeasonId
        };
    }

    private static void AddPhaseSnapshots(
        CompetitionTeamOdds team,
        EspnEventCompetitionOddsTeamOdds t,
        string sourceUrlHash,
        bool sideIsHome)
    {
        Add(team, "Open", t.Open);
        Add(team, "Close", t.Close);
        Add(team, "Current", t.Current);

        void Add(CompetitionTeamOdds teamOdds, string phase, OddsPhaseBlock? p)
        {
            if (p == null) return;

            // Point spread LINE (ESPN often puts line in american/alt; parse from that)
            var pointRaw = p.PointSpread?.American ?? p.PointSpread?.AlternateDisplayValue;
            var pointNum = TryParseDecimal(pointRaw);

            // Phase-aware favorite flags:
            // Prefer explicit p.Favorite when present (seen in some payloads under "open.favorite")
            // Else derive from the line sign (negative => favorite for that side if it's home; positive => the other side).
            bool? fav = p.Favorite;
            if (fav is null && pointNum is not null)
            {
                fav = sideIsHome
                    ? pointNum < 0m
                    : pointNum < 0m ? true : (pointNum > 0m ? (bool?)false : null);
            }

            var snap = new CompetitionTeamOddsSnapshot
            {
                TeamOddsId = teamOdds.Id, // EF sets FK on attach
                Phase = phase,

                // phase-aware favorite status
                IsFavorite = fav,
                IsUnderdog = fav is bool b ? !b : null,

                // spread line
                PointSpreadRaw = pointRaw,
                PointSpreadNum = pointNum,

                // spread PRICE
                SpreadValue = (decimal?)p.Spread?.Value,
                SpreadDisplay = p.Spread?.DisplayValue,
                SpreadAlt = p.Spread?.AlternateDisplayValue,
                SpreadDecimal = (decimal?)p.Spread?.Decimal,
                SpreadFraction = p.Spread?.Fraction,
                SpreadAmerican = p.Spread?.American,
                SpreadOutcome = p.Spread?.Outcome?.Type,

                // moneyline PRICE
                MoneylineValue = (decimal?)p.MoneyLine?.Value,
                MoneylineDisplay = p.MoneyLine?.DisplayValue,
                MoneylineAlt = p.MoneyLine?.AlternateDisplayValue,
                MoneylineDecimal = (decimal?)p.MoneyLine?.Decimal,
                MoneylineFraction = p.MoneyLine?.Fraction,
                MoneylineAmerican = p.MoneyLine?.American,
                MoneylineOutcome = p.MoneyLine?.Outcome?.Type,

                // normalized american ML as int (+100 for "EVEN")
                MoneylineAmericanNum = ParseAmericanInt(p.MoneyLine?.American),

                // provenance (optional)
                SourceUrlHash = sourceUrlHash
            };

            teamOdds.Snapshots.Add(snap);
        }
    }

    private static void AddTotalsSnapshot(
        CompetitionOdds parent,
        string phase,
        OddsPhaseBlock? p,
        string sourceUrlHash)
    {
        if (p == null) return;

        // TOTAL LINE is the number like "47.5" and lives in total.american/alternateDisplayValue
        var totalLineRaw = p.Total?.American ?? p.Total?.AlternateDisplayValue;
        var totalLineNum = TryParseDecimal(totalLineRaw);

        var snap = new CompetitionTotalsSnapshot
        {
            CompetitionOddsId = parent.Id,
            Phase = phase,

            // Over price (+ outcome)
            OverValue = (decimal?)p.Over?.Value,
            OverDisplay = p.Over?.DisplayValue,
            OverAlt = p.Over?.AlternateDisplayValue,
            OverDecimal = (decimal?)p.Over?.Decimal,
            OverFraction = p.Over?.Fraction,
            OverAmerican = p.Over?.American,
            OverOutcome = p.Over?.Outcome?.Type,

            // Under price (+ outcome)
            UnderValue = (decimal?)p.Under?.Value,
            UnderDisplay = p.Under?.DisplayValue,
            UnderAlt = p.Under?.AlternateDisplayValue,
            UnderDecimal = (decimal?)p.Under?.Decimal,
            UnderFraction = p.Under?.Fraction,
            UnderAmerican = p.Under?.American,
            UnderOutcome = p.Under?.Outcome?.Type,

            // Total LINE (raw + parsed)
            TotalValue = totalLineNum,          // parsed from "47.5"
            TotalDisplay = p.Total?.DisplayValue, // usually a price display; keep for fidelity
            TotalAlt = p.Total?.AlternateDisplayValue,
            TotalDecimal = (decimal?)p.Total?.Decimal,
            TotalFraction = p.Total?.Fraction,
            TotalAmerican = totalLineRaw,

            // provenance (optional)
            SourceUrlHash = sourceUrlHash
        };

        parent.Totals.Add(snap);
    }

    // Helpers

    private static decimal? TryParseDecimal(string? s)
        => decimal.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;

    private static int? ParseAmericanInt(string? american)
    {
        if (string.IsNullOrWhiteSpace(american)) return null;
        if (american.Equals("EVEN", StringComparison.OrdinalIgnoreCase)) return 100;

        // supports "+195" / "-230" or "195" / "-230"
        if (int.TryParse(american, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
            return v;

        return null;
    }
}
