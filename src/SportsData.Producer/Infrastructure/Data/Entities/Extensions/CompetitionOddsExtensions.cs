using System.Globalization;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

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
            Id = identity.CanonicalId, // stable per odds provider ref
            CompetitionId = competitionId,

            ProviderRef = src.Provider.Ref,
            ProviderId = src.Provider.Id,
            ProviderName = src.Provider.Name,
            ProviderPriority = src.Provider.Priority,

            Details = src.Details,

            // Headline mirrors (treat as "current" mirrors for convenience)
            OverUnder = src.OverUnder,
            Spread = src.Spread,
            OverOdds = src.OverOdds,
            UnderOdds = src.UnderOdds,

            MoneylineWinner = src.MoneylineWinner,
            SpreadWinner = src.SpreadWinner,

            // Totals: open/current/close
            TotalPointsOpen = ParseDecimalFromLine(src.Open?.Total),
            OverPriceOpen = ParsePrice(src.Open?.Over),
            UnderPriceOpen = ParsePrice(src.Open?.Under),

            TotalPointsCurrent = ParseDecimalFromLine(src.Current?.Total),
            OverPriceCurrent = ParsePrice(src.Current?.Over),
            UnderPriceCurrent = ParsePrice(src.Current?.Under),

            TotalPointsClose = ParseDecimalFromLine(src.Close?.Total),
            OverPriceClose = ParsePrice(src.Close?.Over),
            UnderPriceClose = ParsePrice(src.Close?.Under),

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
                    Rel = l.Rel is { Count: > 0 } ? string.Join(',', l.Rel) : string.Empty,
                    Language = l.Language,
                    Href = l.Href.OriginalString,
                    Text = l.Text,
                    ShortText = l.ShortText,
                    IsExternal = l.IsExternal,
                    IsPremium = l.IsPremium
                });
            }
        }

        // Teams
        e.Teams.Add(BuildTeam(src.HomeTeamOdds, "Home", homeFranchiseSeasonId));
        e.Teams.Add(BuildTeam(src.AwayTeamOdds, "Away", awayFranchiseSeasonId));

        return e;
    }

    private static CompetitionTeamOdds BuildTeam(
        EspnEventCompetitionOddsTeamOdds t,
        string side,
        Guid franchiseSeasonId)
    {
        return new CompetitionTeamOdds
        {
            Side = side,
            FranchiseSeasonId = franchiseSeasonId,

            // Current headline flags
            IsFavorite = t.Favorite,
            IsUnderdog = t.Underdog,

            // Moneyline (American int)
            MoneylineOpen = ParseAmericanInt(t.Open?.MoneyLine?.American ?? t.Open?.MoneyLine?.AlternateDisplayValue),
            MoneylineCurrent = t.MoneyLine ?? ParseAmericanInt(t.Current?.MoneyLine?.American ?? t.Current?.MoneyLine?.AlternateDisplayValue),
            MoneylineClose = ParseAmericanInt(t.Close?.MoneyLine?.American ?? t.Close?.MoneyLine?.AlternateDisplayValue),

            // Spread points (team-relative; from pointSpread)
            SpreadPointsOpen = GetSpreadPoints(t.Open),
            SpreadPointsCurrent = GetSpreadPoints(t.Current),
            SpreadPointsClose = GetSpreadPoints(t.Close),

            // Spread price (vig; American, e.g., -110)
            SpreadPriceOpen = ParsePrice(t.Open?.Spread),
            SpreadPriceCurrent = t.SpreadOdds ?? ParsePrice(t.Current?.Spread),
            SpreadPriceClose = ParsePrice(t.Close?.Spread)
        };
    }

    // ----- Helpers -----
    private static decimal? GetSpreadPoints(OddsPhaseBlock? phase)
    {
        if (phase?.PointSpread is null) return null;

        // ESPN often puts the *line* in .american or .alternateDisplayValue for pointSpread
        var s = phase.PointSpread.American ?? phase.PointSpread.AlternateDisplayValue ?? phase.PointSpread.DisplayValue;
        return ParseSignedDecimal(s);
    }

    // Optional: if you want a robust fallback when a team's phase is missing pointSpread, 
    // derive from the *other* team or from the headline spread (home-relative). 
    // Keep it explicit so we don't blend price with line.
    private static (decimal? home, decimal? away) DeriveFromHeadline(decimal? headlineHomeSpread)
    {
        if (headlineHomeSpread is null) return (null, null);
        var h = headlineHomeSpread.Value;
        return (h, -h);
    }


    // For totals "line" (e.g., 53.5) ESPN puts the number in .american or .alternateDisplayValue
    private static decimal? ParseDecimalFromLine(PriceBlock? line)
        => ParseSignedDecimal(line?.American ?? line?.AlternateDisplayValue);

    // American price (e.g., "-110", "+100", "EVEN") as decimal
    private static decimal? ParsePrice(PriceBlock? p)
    {
        if (p == null) return null;
        var s = p.American ?? p.AlternateDisplayValue ?? p.DisplayValue;
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (s.Equals("EVEN", StringComparison.OrdinalIgnoreCase))
            return 100m;

        if (decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
            return d;

        return null;
    }

    private static decimal? ParseSignedDecimal(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        if (decimal.TryParse(s, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var d))
            return d;
        return null;
    }

    private static int? ParseAmericanInt(string? american)
    {
        if (string.IsNullOrWhiteSpace(american)) return null;
        if (american.Equals("EVEN", StringComparison.OrdinalIgnoreCase)) return 100;

        if (int.TryParse(american, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var v))
            return v;

        return null;
    }
}
