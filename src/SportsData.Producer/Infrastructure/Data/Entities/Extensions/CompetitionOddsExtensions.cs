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
            Id = identity.CanonicalId,   // stable per $ref (…/competitions/{id}/odds/{providerId})
            CompetitionId = competitionId,
            ProviderRef = src.Provider.Ref,
            ProviderId = src.Provider.Id,
            ProviderName = src.Provider.Name,
            ProviderPriority = src.Provider.Priority,
            Details = src.Details,
            OverUnder = (decimal?)src.OverUnder,
            Spread = (decimal?)src.Spread,
            OverOdds = (decimal?)src.OverOdds,
            UnderOdds = (decimal?)src.UnderOdds,
            MoneylineWinner = src.MoneylineWinner,
            SpreadWinner = src.SpreadWinner,
            ContentHash = contentHash,
            CreatedBy = correlationId,
            CreatedUtc = DateTime.UtcNow,
            ExternalIds = new List<CompetitionOddsExternalId>
            {
                new()
                {
                    Id            = identity.CanonicalId,
                    Value         = identity.UrlHash,
                    Provider      = SourceDataProvider.Espn,
                    SourceUrlHash = identity.UrlHash,
                    SourceUrl     = identity.CleanUrl
                }
            }
        };

        // Teams (Home / Away)
        var home = BuildTeam(src.HomeTeamOdds, "Home", homeFranchiseSeasonId);
        var away = BuildTeam(src.AwayTeamOdds, "Away", awayFranchiseSeasonId);
        e.Teams.Add(home);
        e.Teams.Add(away);

        AddPhaseSnapshots(home, src.HomeTeamOdds);
        AddPhaseSnapshots(away, src.AwayTeamOdds);

        // Totals snapshots (top-level)
        AddTotalsSnapshot(e, "Open", src.Open);
        AddTotalsSnapshot(e, "Close", src.Close);
        AddTotalsSnapshot(e, "Current", src.Current);

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
            IsFavorite = t.Favorite,
            IsUnderdog = t.Underdog,
            HeadlineMoneyLine = t.MoneyLine,
            HeadlineSpreadOdds = (decimal?)t.SpreadOdds,
            FranchiseSeasonId = franchiseSeasonId
        };
    }


    private static void AddPhaseSnapshots(CompetitionTeamOdds team, EspnEventCompetitionOddsTeamOdds t)
    {
        Add(team, "Open", t.Open);
        Add(team, "Close", t.Close);
        Add(team, "Current", t.Current);

        static void Add(CompetitionTeamOdds team, string phase, OddsPhaseBlock? p)
        {
            if (p == null) return;

            // Point spread raw/num
            var pointRaw = p.PointSpread?.American ?? p.PointSpread?.AlternateDisplayValue;
            decimal? pointNum = null;
            if (decimal.TryParse(pointRaw, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d))
                pointNum = d;

            var snap = new CompetitionTeamOddsSnapshot
            {
                TeamOddsId = team.Id, // EF will set after attach; key not needed now
                Phase = phase,
                PointSpreadRaw = pointRaw,
                PointSpreadNum = pointNum,

                // Spread price
                SpreadValue = (decimal?)p.Spread?.Value,
                SpreadDisplay = p.Spread?.DisplayValue,
                SpreadAlt = p.Spread?.AlternateDisplayValue,
                SpreadDecimal = (decimal?)p.Spread?.Decimal,
                SpreadFraction = p.Spread?.Fraction,
                SpreadAmerican = p.Spread?.American,
                SpreadOutcome = p.Spread?.Outcome?.Type,

                // Moneyline price
                MoneylineValue = (decimal?)p.MoneyLine?.Value,
                MoneylineDisplay = p.MoneyLine?.DisplayValue,
                MoneylineAlt = p.MoneyLine?.AlternateDisplayValue,
                MoneylineDecimal = (decimal?)p.MoneyLine?.Decimal,
                MoneylineFraction = p.MoneyLine?.Fraction,
                MoneylineAmerican = p.MoneyLine?.American,
                MoneylineOutcome = p.MoneyLine?.Outcome?.Type
            };

            team.Snapshots.Add(snap);
        }
    }

    private static void AddTotalsSnapshot(CompetitionOdds parent, string phase, OddsPhaseBlock? p)
    {
        if (p == null) return;

        var snap = new CompetitionTotalsSnapshot
        {
            CompetitionOddsId = parent.Id, // EF sets after attach
            Phase = phase,

            OverValue = (decimal?)p.Over?.Value,
            OverDisplay = p.Over?.DisplayValue,
            OverAlt = p.Over?.AlternateDisplayValue,
            OverDecimal = (decimal?)p.Over?.Decimal,
            OverFraction = p.Over?.Fraction,
            OverAmerican = p.Over?.American,
            OverOutcome = p.Over?.Outcome?.Type,

            UnderValue = (decimal?)p.Under?.Value,
            UnderDisplay = p.Under?.DisplayValue,
            UnderAlt = p.Under?.AlternateDisplayValue,
            UnderDecimal = (decimal?)p.Under?.Decimal,
            UnderFraction = p.Under?.Fraction,
            UnderAmerican = p.Under?.American,
            UnderOutcome = p.Under?.Outcome?.Type,

            // Total line (ESPN puts the number in alt/american commonly)
            TotalValue = (decimal?)p.Total?.Value,
            TotalDisplay = p.Total?.DisplayValue,
            TotalAlt = p.Total?.AlternateDisplayValue,
            TotalDecimal = (decimal?)p.Total?.Decimal,
            TotalFraction = p.Total?.Fraction,
            TotalAmerican = p.Total?.American
        };

        parent.Totals.Add(snap);
    }
}