using SportsData.Core.Infrastructure.DataSources.Espn.Dtos.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities.Extensions
{
    public static class ContestOddsExtensions
    {
        public static ContestOdds AsEntity(
            this EspnEventCompetitionOddsDto dto,
            Guid contestId,
            Guid homeTeamFranchiseSeasonId,
            Guid awayTeamFranchiseSeasonId)
        {
            var home = dto.HomeTeamOdds;
            var away = dto.AwayTeamOdds;

            return new ContestOdds
            {
                Id = Guid.NewGuid(),
                ContestId = contestId,

                // Provider
                ProviderRef = dto.Provider?.Ref is not null ? new Uri(dto.Provider.Ref) : dto.Ref,
                ProviderId = dto.Provider?.Id ?? string.Empty,
                ProviderName = dto.Provider?.Name ?? string.Empty,
                ProviderPriority = dto.Provider?.Priority ?? 0,

                // General Odds Info
                Details = dto.Details,
                OverUnder = (decimal?)dto.OverUnder,
                Spread = (decimal?)dto.Spread,
                OverOdds = dto.OverOdds,
                UnderOdds = dto.UnderOdds,
                MoneylineWinner = dto.MoneylineWinner,
                SpreadWinner = dto.SpreadWinner,

                // Home Team - Core
                HomeTeamFavorite = home?.Favorite ?? false,
                HomeTeamUnderdog = home?.Underdog ?? false,
                HomeTeamMoneyLine = home?.MoneyLine,
                HomeTeamSpreadOdds = home?.SpreadOdds,
                HomeTeamFranchiseSeasonId = homeTeamFranchiseSeasonId,

                // Away Team - Core
                AwayTeamFavorite = away?.Favorite ?? false,
                AwayTeamUnderdog = away?.Underdog ?? false,
                AwayTeamMoneyLine = away?.MoneyLine,
                AwayTeamSpreadOdds = away?.SpreadOdds,
                AwayTeamFranchiseSeasonId = awayTeamFranchiseSeasonId,

                // Open Market - Over/Under/Total
                OpenOverValue = (decimal?)dto.Open?.Over?.Value,
                OpenOverDisplayValue = dto.Open?.Over?.DisplayValue,
                OpenOverAlternateDisplayValue = dto.Open?.Over?.AlternateDisplayValue,
                OpenOverDecimal = (decimal?)dto.Open?.Over?.Decimal,
                OpenOverFraction = dto.Open?.Over?.Fraction,
                OpenOverAmerican = dto.Open?.Over?.American,

                OpenUnderValue = (decimal?)dto.Open?.Under?.Value,
                OpenUnderDisplayValue = dto.Open?.Under?.DisplayValue,
                OpenUnderAlternateDisplayValue = dto.Open?.Under?.AlternateDisplayValue,
                OpenUnderDecimal = (decimal?)dto.Open?.Under?.Decimal,
                OpenUnderFraction = dto.Open?.Under?.Fraction,
                OpenUnderAmerican = dto.Open?.Under?.American,

                OpenTotalValue = (decimal?)dto.Open?.Total?.Value,
                OpenTotalDisplayValue = dto.Open?.Total?.DisplayValue,
                OpenTotalAlternateDisplayValue = dto.Open?.Total?.AlternateDisplayValue,
                OpenTotalDecimal = (decimal?)dto.Open?.Total?.Decimal,
                OpenTotalFraction = dto.Open?.Total?.Fraction,
                OpenTotalAmerican = dto.Open?.Total?.American,

                // Away Team - Open
                AwayTeamOpenFavorite = away?.Open?.Favorite ?? false,
                AwayTeamOpenPointSpreadAlternateDisplayValue = away?.Open?.PointSpread?.AlternateDisplayValue,
                AwayTeamOpenPointSpreadAmerican = away?.Open?.PointSpread?.American,
                AwayTeamOpenSpreadValue = (decimal?)away?.Open?.Spread?.Value,
                AwayTeamOpenSpreadDisplayValue = away?.Open?.Spread?.DisplayValue,
                AwayTeamOpenSpreadAlternateDisplayValue = away?.Open?.Spread?.AlternateDisplayValue,
                AwayTeamOpenSpreadDecimal = (decimal?)away?.Open?.Spread?.Decimal,
                AwayTeamOpenSpreadFraction = away?.Open?.Spread?.Fraction,
                AwayTeamOpenSpreadAmerican = away?.Open?.Spread?.American,
                AwayTeamOpenMoneyLineValue = (decimal?)away?.Open?.MoneyLine?.Value,
                AwayTeamOpenMoneyLineDisplayValue = away?.Open?.MoneyLine?.DisplayValue,
                AwayTeamOpenMoneyLineAlternateDisplayValue = away?.Open?.MoneyLine?.AlternateDisplayValue,
                AwayTeamOpenMoneyLineDecimal = (decimal?)away?.Open?.MoneyLine?.Decimal,
                AwayTeamOpenMoneyLineFraction = away?.Open?.MoneyLine?.Fraction,
                AwayTeamOpenMoneyLineAmerican = away?.Open?.MoneyLine?.American,

                // Home Team - Open
                HomeTeamOpenFavorite = home?.Open?.Favorite ?? false,
                HomeTeamOpenPointSpreadValue = (decimal?)home?.Open?.PointSpread?.Value,
                HomeTeamOpenPointSpreadDisplayValue = home?.Open?.PointSpread?.DisplayValue,
                HomeTeamOpenPointSpreadAlternateDisplayValue = home?.Open?.PointSpread?.AlternateDisplayValue,
                HomeTeamOpenPointSpreadDecimal = (decimal?)home?.Open?.PointSpread?.Decimal,
                HomeTeamOpenPointSpreadFraction = home?.Open?.PointSpread?.Fraction,
                HomeTeamOpenPointSpreadAmerican = home?.Open?.PointSpread?.American,
                HomeTeamOpenSpreadValue = (decimal?)home?.Open?.Spread?.Value,
                HomeTeamOpenSpreadDisplayValue = home?.Open?.Spread?.DisplayValue,
                HomeTeamOpenSpreadAlternateDisplayValue = home?.Open?.Spread?.AlternateDisplayValue,
                HomeTeamOpenSpreadDecimal = (decimal?)home?.Open?.Spread?.Decimal,
                HomeTeamOpenSpreadFraction = home?.Open?.Spread?.Fraction,
                HomeTeamOpenSpreadAmerican = home?.Open?.Spread?.American,
                HomeTeamOpenMoneyLineValue = (decimal?)home?.Open?.MoneyLine?.Value,
                HomeTeamOpenMoneyLineDisplayValue = home?.Open?.MoneyLine?.DisplayValue,
                HomeTeamOpenMoneyLineAlternateDisplayValue = home?.Open?.MoneyLine?.AlternateDisplayValue,
                HomeTeamOpenMoneyLineDecimal = (decimal?)home?.Open?.MoneyLine?.Decimal,
                HomeTeamOpenMoneyLineFraction = home?.Open?.MoneyLine?.Fraction,
                HomeTeamOpenMoneyLineAmerican = home?.Open?.MoneyLine?.American,

                // Away Team - Close
                AwayTeamClosePointSpreadAlternateDisplayValue = away?.Close?.PointSpread?.AlternateDisplayValue,
                AwayTeamClosePointSpreadAmerican = away?.Close?.PointSpread?.American,
                AwayTeamCloseSpreadValue = (decimal?)away?.Close?.Spread?.Value,
                AwayTeamCloseSpreadDisplayValue = away?.Close?.Spread?.DisplayValue,
                AwayTeamCloseSpreadAlternateDisplayValue = away?.Close?.Spread?.AlternateDisplayValue,
                AwayTeamCloseSpreadDecimal = (decimal?)away?.Close?.Spread?.Decimal,
                AwayTeamCloseSpreadFraction = away?.Close?.Spread?.Fraction,
                AwayTeamCloseSpreadAmerican = away?.Close?.Spread?.American,
                AwayTeamCloseMoneyLineValue = (decimal?)away?.Close?.MoneyLine?.Value,
                AwayTeamCloseMoneyLineDisplayValue = away?.Close?.MoneyLine?.DisplayValue,
                AwayTeamCloseMoneyLineAlternateDisplayValue = away?.Close?.MoneyLine?.AlternateDisplayValue,
                AwayTeamCloseMoneyLineDecimal = (decimal?)away?.Close?.MoneyLine?.Decimal,
                AwayTeamCloseMoneyLineFraction = away?.Close?.MoneyLine?.Fraction,
                AwayTeamCloseMoneyLineAmerican = away?.Close?.MoneyLine?.American,

                // Home Team - Close
                HomeTeamClosePointSpreadAlternateDisplayValue = home?.Close?.PointSpread?.AlternateDisplayValue,
                HomeTeamClosePointSpreadAmerican = home?.Close?.PointSpread?.American,
                HomeTeamCloseSpreadValue = (decimal?)home?.Close?.Spread?.Value,
                HomeTeamCloseSpreadDisplayValue = home?.Close?.Spread?.DisplayValue,
                HomeTeamCloseSpreadAlternateDisplayValue = home?.Close?.Spread?.AlternateDisplayValue,
                HomeTeamCloseSpreadDecimal = (decimal?)home?.Close?.Spread?.Decimal,
                HomeTeamCloseSpreadFraction = home?.Close?.Spread?.Fraction,
                HomeTeamCloseSpreadAmerican = home?.Close?.Spread?.American,
                HomeTeamCloseMoneyLineValue = (decimal?)home?.Close?.MoneyLine?.Value,
                HomeTeamCloseMoneyLineDisplayValue = home?.Close?.MoneyLine?.DisplayValue,
                HomeTeamCloseMoneyLineAlternateDisplayValue = home?.Close?.MoneyLine?.AlternateDisplayValue,
                HomeTeamCloseMoneyLineDecimal = (decimal?)home?.Close?.MoneyLine?.Decimal,
                HomeTeamCloseMoneyLineFraction = home?.Close?.MoneyLine?.Fraction,
                HomeTeamCloseMoneyLineAmerican = home?.Close?.MoneyLine?.American,

                // Close Market - Over/Under/Total
                CloseOverValue = (decimal?)dto.Close?.Over?.Value,
                CloseOverDisplayValue = dto.Close?.Over?.DisplayValue,
                CloseOverAlternateDisplayValue = dto.Close?.Over?.AlternateDisplayValue,
                CloseOverDecimal = (decimal?)dto.Close?.Over?.Decimal,
                CloseOverFraction = dto.Close?.Over?.Fraction,
                CloseOverAmerican = dto.Close?.Over?.American,

                CloseUnderValue = (decimal?)dto.Close?.Under?.Value,
                CloseUnderDisplayValue = dto.Close?.Under?.DisplayValue,
                CloseUnderAlternateDisplayValue = dto.Close?.Under?.AlternateDisplayValue,
                CloseUnderDecimal = (decimal?)dto.Close?.Under?.Decimal,
                CloseUnderFraction = dto.Close?.Under?.Fraction,
                CloseUnderAmerican = dto.Close?.Under?.American,

                CloseTotalValue = (decimal?)dto.Close?.Total?.Value,
                CloseTotalDisplayValue = dto.Close?.Total?.DisplayValue,
                CloseTotalAlternateDisplayValue = dto.Close?.Total?.AlternateDisplayValue,
                CloseTotalDecimal = (decimal?)dto.Close?.Total?.Decimal,
                CloseTotalFraction = dto.Close?.Total?.Fraction,
                CloseTotalAmerican = dto.Close?.Total?.American,

                // Away Team - Current
                AwayTeamCurrentPointSpreadAlternateDisplayValue = away?.Current?.PointSpread?.AlternateDisplayValue,
                AwayTeamCurrentPointSpreadAmerican = away?.Current?.PointSpread?.American,
                AwayTeamCurrentSpreadValue = (decimal?)away?.Current?.Spread?.Value,
                AwayTeamCurrentSpreadDisplayValue = away?.Current?.Spread?.DisplayValue,
                AwayTeamCurrentSpreadAlternateDisplayValue = away?.Current?.Spread?.AlternateDisplayValue,
                AwayTeamCurrentSpreadDecimal = (decimal?)away?.Current?.Spread?.Decimal,
                AwayTeamCurrentSpreadFraction = away?.Current?.Spread?.Fraction,
                AwayTeamCurrentSpreadAmerican = away?.Current?.Spread?.American,
                AwayTeamCurrentSpreadOutcomeType = away?.Current?.Spread?.Outcome?.Type,
                AwayTeamCurrentMoneyLineValue = (decimal?)away?.Current?.MoneyLine?.Value,
                AwayTeamCurrentMoneyLineDisplayValue = away?.Current?.MoneyLine?.DisplayValue,
                AwayTeamCurrentMoneyLineAlternateDisplayValue = away?.Current?.MoneyLine?.AlternateDisplayValue,
                AwayTeamCurrentMoneyLineDecimal = (decimal?)away?.Current?.MoneyLine?.Decimal,
                AwayTeamCurrentMoneyLineFraction = away?.Current?.MoneyLine?.Fraction,
                AwayTeamCurrentMoneyLineAmerican = away?.Current?.MoneyLine?.American,
                AwayTeamCurrentMoneyLineOutcomeType = away?.Current?.MoneyLine?.Outcome?.Type,

                // Home Team - Current
                HomeTeamCurrentPointSpreadAlternateDisplayValue = home?.Current?.PointSpread?.AlternateDisplayValue,
                HomeTeamCurrentPointSpreadAmerican = home?.Current?.PointSpread?.American,
                HomeTeamCurrentSpreadValue = (decimal?)home?.Current?.Spread?.Value,
                HomeTeamCurrentSpreadDisplayValue = home?.Current?.Spread?.DisplayValue,
                HomeTeamCurrentSpreadAlternateDisplayValue = home?.Current?.Spread?.AlternateDisplayValue,
                HomeTeamCurrentSpreadDecimal = (decimal?)home?.Current?.Spread?.Decimal,
                HomeTeamCurrentSpreadFraction = home?.Current?.Spread?.Fraction,
                HomeTeamCurrentSpreadAmerican = home?.Current?.Spread?.American,
                HomeTeamCurrentSpreadOutcomeType = home?.Current?.Spread?.Outcome?.Type,
                HomeTeamCurrentMoneyLineValue = (decimal?)home?.Current?.MoneyLine?.Value,
                HomeTeamCurrentMoneyLineDisplayValue = home?.Current?.MoneyLine?.DisplayValue,
                HomeTeamCurrentMoneyLineAlternateDisplayValue = home?.Current?.MoneyLine?.AlternateDisplayValue,
                HomeTeamCurrentMoneyLineDecimal = (decimal?)home?.Current?.MoneyLine?.Decimal,
                HomeTeamCurrentMoneyLineFraction = home?.Current?.MoneyLine?.Fraction,
                HomeTeamCurrentMoneyLineAmerican = home?.Current?.MoneyLine?.American,
                HomeTeamCurrentMoneyLineOutcomeType = home?.Current?.MoneyLine?.Outcome?.Type,

                // Current Market - Over/Under/Total
                CurrentOverValue = (decimal?)dto.Current?.Over?.Value,
                CurrentOverDisplayValue = dto.Current?.Over?.DisplayValue,
                CurrentOverAlternateDisplayValue = dto.Current?.Over?.AlternateDisplayValue,
                CurrentOverDecimal = (decimal?)dto.Current?.Over?.Decimal,
                CurrentOverFraction = dto.Current?.Over?.Fraction,
                CurrentOverAmerican = dto.Current?.Over?.American,
                CurrentOverOutcomeType = dto.Current?.Over?.Outcome?.Type,

                CurrentUnderValue = (decimal?)dto.Current?.Under?.Value,
                CurrentUnderDisplayValue = dto.Current?.Under?.DisplayValue,
                CurrentUnderAlternateDisplayValue = dto.Current?.Under?.AlternateDisplayValue,
                CurrentUnderDecimal = (decimal?)dto.Current?.Under?.Decimal,
                CurrentUnderFraction = dto.Current?.Under?.Fraction,
                CurrentUnderAmerican = dto.Current?.Under?.American,
                CurrentUnderOutcomeType = dto.Current?.Under?.Outcome?.Type,

                CurrentTotalValue = (decimal?)dto.Current?.Total?.Value,
                CurrentTotalDisplayValue = dto.Current?.Total?.DisplayValue,
                CurrentTotalAlternateDisplayValue = dto.Current?.Total?.AlternateDisplayValue,
                CurrentTotalDecimal = (decimal?)dto.Current?.Total?.Decimal,
                CurrentTotalFraction = dto.Current?.Total?.Fraction,
                CurrentTotalAmerican = dto.Current?.Total?.American,

            };
        }

        public static bool HasDifferences(this ContestOdds existing, ContestOdds incoming)
        {
            if (existing == null || incoming == null) return true;

            return
                existing.ProviderId != incoming.ProviderId ||
                existing.ProviderName != incoming.ProviderName ||
                existing.ProviderPriority != incoming.ProviderPriority ||
                existing.Details != incoming.Details ||
                existing.OverUnder != incoming.OverUnder ||
                existing.Spread != incoming.Spread ||
                existing.OverOdds != incoming.OverOdds ||
                existing.UnderOdds != incoming.UnderOdds ||
                existing.MoneylineWinner != incoming.MoneylineWinner ||
                existing.SpreadWinner != incoming.SpreadWinner ||

                existing.HomeTeamFavorite != incoming.HomeTeamFavorite ||
                existing.HomeTeamUnderdog != incoming.HomeTeamUnderdog ||
                existing.HomeTeamMoneyLine != incoming.HomeTeamMoneyLine ||
                existing.HomeTeamSpreadOdds != incoming.HomeTeamSpreadOdds ||

                existing.AwayTeamFavorite != incoming.AwayTeamFavorite ||
                existing.AwayTeamUnderdog != incoming.AwayTeamUnderdog ||
                existing.AwayTeamMoneyLine != incoming.AwayTeamMoneyLine ||
                existing.AwayTeamSpreadOdds != incoming.AwayTeamSpreadOdds ||

                existing.OpenOverValue != incoming.OpenOverValue ||
                existing.OpenOverDisplayValue != incoming.OpenOverDisplayValue ||
                existing.OpenOverAlternateDisplayValue != incoming.OpenOverAlternateDisplayValue ||
                existing.OpenOverDecimal != incoming.OpenOverDecimal ||
                existing.OpenOverFraction != incoming.OpenOverFraction ||
                existing.OpenOverAmerican != incoming.OpenOverAmerican ||

                existing.OpenUnderValue != incoming.OpenUnderValue ||
                existing.OpenUnderDisplayValue != incoming.OpenUnderDisplayValue ||
                existing.OpenUnderAlternateDisplayValue != incoming.OpenUnderAlternateDisplayValue ||
                existing.OpenUnderDecimal != incoming.OpenUnderDecimal ||
                existing.OpenUnderFraction != incoming.OpenUnderFraction ||
                existing.OpenUnderAmerican != incoming.OpenUnderAmerican ||

                existing.OpenTotalValue != incoming.OpenTotalValue ||
                existing.OpenTotalDisplayValue != incoming.OpenTotalDisplayValue ||
                existing.OpenTotalAlternateDisplayValue != incoming.OpenTotalAlternateDisplayValue ||
                existing.OpenTotalDecimal != incoming.OpenTotalDecimal ||
                existing.OpenTotalFraction != incoming.OpenTotalFraction ||
                existing.OpenTotalAmerican != incoming.OpenTotalAmerican ||

                // Away Team Open
                existing.AwayTeamOpenFavorite != incoming.AwayTeamOpenFavorite ||
                existing.AwayTeamOpenPointSpreadAlternateDisplayValue !=
                incoming.AwayTeamOpenPointSpreadAlternateDisplayValue ||
                existing.AwayTeamOpenPointSpreadAmerican != incoming.AwayTeamOpenPointSpreadAmerican ||
                existing.AwayTeamOpenSpreadValue != incoming.AwayTeamOpenSpreadValue ||
                existing.AwayTeamOpenSpreadDisplayValue != incoming.AwayTeamOpenSpreadDisplayValue ||
                existing.AwayTeamOpenSpreadAlternateDisplayValue != incoming.AwayTeamOpenSpreadAlternateDisplayValue ||
                existing.AwayTeamOpenSpreadDecimal != incoming.AwayTeamOpenSpreadDecimal ||
                existing.AwayTeamOpenSpreadFraction != incoming.AwayTeamOpenSpreadFraction ||
                existing.AwayTeamOpenSpreadAmerican != incoming.AwayTeamOpenSpreadAmerican ||
                existing.AwayTeamOpenMoneyLineValue != incoming.AwayTeamOpenMoneyLineValue ||
                existing.AwayTeamOpenMoneyLineDisplayValue != incoming.AwayTeamOpenMoneyLineDisplayValue ||
                existing.AwayTeamOpenMoneyLineAlternateDisplayValue !=
                incoming.AwayTeamOpenMoneyLineAlternateDisplayValue ||
                existing.AwayTeamOpenMoneyLineDecimal != incoming.AwayTeamOpenMoneyLineDecimal ||
                existing.AwayTeamOpenMoneyLineFraction != incoming.AwayTeamOpenMoneyLineFraction ||
                existing.AwayTeamOpenMoneyLineAmerican != incoming.AwayTeamOpenMoneyLineAmerican ||

                // Home Team Open
                existing.HomeTeamOpenFavorite != incoming.HomeTeamOpenFavorite ||
                existing.HomeTeamOpenPointSpreadValue != incoming.HomeTeamOpenPointSpreadValue ||
                existing.HomeTeamOpenPointSpreadDisplayValue != incoming.HomeTeamOpenPointSpreadDisplayValue ||
                existing.HomeTeamOpenPointSpreadAlternateDisplayValue !=
                incoming.HomeTeamOpenPointSpreadAlternateDisplayValue ||
                existing.HomeTeamOpenPointSpreadDecimal != incoming.HomeTeamOpenPointSpreadDecimal ||
                existing.HomeTeamOpenPointSpreadFraction != incoming.HomeTeamOpenPointSpreadFraction ||
                existing.HomeTeamOpenPointSpreadAmerican != incoming.HomeTeamOpenPointSpreadAmerican ||
                existing.HomeTeamOpenSpreadValue != incoming.HomeTeamOpenSpreadValue ||
                existing.HomeTeamOpenSpreadDisplayValue != incoming.HomeTeamOpenSpreadDisplayValue ||
                existing.HomeTeamOpenSpreadAlternateDisplayValue != incoming.HomeTeamOpenSpreadAlternateDisplayValue ||
                existing.HomeTeamOpenSpreadDecimal != incoming.HomeTeamOpenSpreadDecimal ||
                existing.HomeTeamOpenSpreadFraction != incoming.HomeTeamOpenSpreadFraction ||
                existing.HomeTeamOpenSpreadAmerican != incoming.HomeTeamOpenSpreadAmerican ||
                existing.HomeTeamOpenMoneyLineValue != incoming.HomeTeamOpenMoneyLineValue ||
                existing.HomeTeamOpenMoneyLineDisplayValue != incoming.HomeTeamOpenMoneyLineDisplayValue ||
                existing.HomeTeamOpenMoneyLineAlternateDisplayValue !=
                incoming.HomeTeamOpenMoneyLineAlternateDisplayValue ||
                existing.HomeTeamOpenMoneyLineDecimal != incoming.HomeTeamOpenMoneyLineDecimal ||
                existing.HomeTeamOpenMoneyLineFraction != incoming.HomeTeamOpenMoneyLineFraction ||
                existing.HomeTeamOpenMoneyLineAmerican != incoming.HomeTeamOpenMoneyLineAmerican ||

                // Away Team Close
                existing.AwayTeamClosePointSpreadAlternateDisplayValue !=
                incoming.AwayTeamClosePointSpreadAlternateDisplayValue ||
                existing.AwayTeamClosePointSpreadAmerican != incoming.AwayTeamClosePointSpreadAmerican ||
                existing.AwayTeamCloseSpreadValue != incoming.AwayTeamCloseSpreadValue ||
                existing.AwayTeamCloseSpreadDisplayValue != incoming.AwayTeamCloseSpreadDisplayValue ||
                existing.AwayTeamCloseSpreadAlternateDisplayValue !=
                incoming.AwayTeamCloseSpreadAlternateDisplayValue ||
                existing.AwayTeamCloseSpreadDecimal != incoming.AwayTeamCloseSpreadDecimal ||
                existing.AwayTeamCloseSpreadFraction != incoming.AwayTeamCloseSpreadFraction ||
                existing.AwayTeamCloseSpreadAmerican != incoming.AwayTeamCloseSpreadAmerican ||
                existing.AwayTeamCloseMoneyLineValue != incoming.AwayTeamCloseMoneyLineValue ||
                existing.AwayTeamCloseMoneyLineDisplayValue != incoming.AwayTeamCloseMoneyLineDisplayValue ||
                existing.AwayTeamCloseMoneyLineAlternateDisplayValue !=
                incoming.AwayTeamCloseMoneyLineAlternateDisplayValue ||
                existing.AwayTeamCloseMoneyLineDecimal != incoming.AwayTeamCloseMoneyLineDecimal ||
                existing.AwayTeamCloseMoneyLineFraction != incoming.AwayTeamCloseMoneyLineFraction ||
                existing.AwayTeamCloseMoneyLineAmerican != incoming.AwayTeamCloseMoneyLineAmerican ||
                // Home Team Close
                existing.HomeTeamClosePointSpreadAlternateDisplayValue !=
                incoming.HomeTeamClosePointSpreadAlternateDisplayValue ||
                existing.HomeTeamClosePointSpreadAmerican != incoming.HomeTeamClosePointSpreadAmerican ||
                existing.HomeTeamCloseSpreadValue != incoming.HomeTeamCloseSpreadValue ||
                existing.HomeTeamCloseSpreadDisplayValue != incoming.HomeTeamCloseSpreadDisplayValue ||
                existing.HomeTeamCloseSpreadAlternateDisplayValue !=
                incoming.HomeTeamCloseSpreadAlternateDisplayValue ||
                existing.HomeTeamCloseSpreadDecimal != incoming.HomeTeamCloseSpreadDecimal ||
                existing.HomeTeamCloseSpreadFraction != incoming.HomeTeamCloseSpreadFraction ||
                existing.HomeTeamCloseSpreadAmerican != incoming.HomeTeamCloseSpreadAmerican ||
                existing.HomeTeamCloseMoneyLineValue != incoming.HomeTeamCloseMoneyLineValue ||
                existing.HomeTeamCloseMoneyLineDisplayValue != incoming.HomeTeamCloseMoneyLineDisplayValue ||
                existing.HomeTeamCloseMoneyLineAlternateDisplayValue !=
                incoming.HomeTeamCloseMoneyLineAlternateDisplayValue ||
                existing.HomeTeamCloseMoneyLineDecimal != incoming.HomeTeamCloseMoneyLineDecimal ||
                existing.HomeTeamCloseMoneyLineFraction != incoming.HomeTeamCloseMoneyLineFraction ||
                existing.HomeTeamCloseMoneyLineAmerican != incoming.HomeTeamCloseMoneyLineAmerican ||

                // Away Team Current
                existing.AwayTeamCurrentPointSpreadAlternateDisplayValue !=
                incoming.AwayTeamCurrentPointSpreadAlternateDisplayValue ||
                existing.AwayTeamCurrentPointSpreadAmerican != incoming.AwayTeamCurrentPointSpreadAmerican ||
                existing.AwayTeamCurrentSpreadValue != incoming.AwayTeamCurrentSpreadValue ||
                existing.AwayTeamCurrentSpreadDisplayValue != incoming.AwayTeamCurrentSpreadDisplayValue ||
                existing.AwayTeamCurrentSpreadAlternateDisplayValue !=
                incoming.AwayTeamCurrentSpreadAlternateDisplayValue ||
                existing.AwayTeamCurrentSpreadDecimal != incoming.AwayTeamCurrentSpreadDecimal ||
                existing.AwayTeamCurrentSpreadFraction != incoming.AwayTeamCurrentSpreadFraction ||
                existing.AwayTeamCurrentSpreadAmerican != incoming.AwayTeamCurrentSpreadAmerican ||
                existing.AwayTeamCurrentSpreadOutcomeType != incoming.AwayTeamCurrentSpreadOutcomeType ||
                existing.AwayTeamCurrentMoneyLineValue != incoming.AwayTeamCurrentMoneyLineValue ||
                existing.AwayTeamCurrentMoneyLineDisplayValue != incoming.AwayTeamCurrentMoneyLineDisplayValue ||
                existing.AwayTeamCurrentMoneyLineAlternateDisplayValue !=
                incoming.AwayTeamCurrentMoneyLineAlternateDisplayValue ||
                existing.AwayTeamCurrentMoneyLineDecimal != incoming.AwayTeamCurrentMoneyLineDecimal ||
                existing.AwayTeamCurrentMoneyLineFraction != incoming.AwayTeamCurrentMoneyLineFraction ||
                existing.AwayTeamCurrentMoneyLineAmerican != incoming.AwayTeamCurrentMoneyLineAmerican ||
                existing.AwayTeamCurrentMoneyLineOutcomeType != incoming.AwayTeamCurrentMoneyLineOutcomeType ||

                // Home Team Current
                existing.HomeTeamCurrentPointSpreadAlternateDisplayValue !=
                incoming.HomeTeamCurrentPointSpreadAlternateDisplayValue ||
                existing.HomeTeamCurrentPointSpreadAmerican != incoming.HomeTeamCurrentPointSpreadAmerican ||
                existing.HomeTeamCurrentSpreadValue != incoming.HomeTeamCurrentSpreadValue ||
                existing.HomeTeamCurrentSpreadDisplayValue != incoming.HomeTeamCurrentSpreadDisplayValue ||
                existing.HomeTeamCurrentSpreadAlternateDisplayValue !=
                incoming.HomeTeamCurrentSpreadAlternateDisplayValue ||
                existing.HomeTeamCurrentSpreadDecimal != incoming.HomeTeamCurrentSpreadDecimal ||
                existing.HomeTeamCurrentSpreadFraction != incoming.HomeTeamCurrentSpreadFraction ||
                existing.HomeTeamCurrentSpreadAmerican != incoming.HomeTeamCurrentSpreadAmerican ||
                existing.HomeTeamCurrentSpreadOutcomeType != incoming.HomeTeamCurrentSpreadOutcomeType ||
                existing.HomeTeamCurrentMoneyLineValue != incoming.HomeTeamCurrentMoneyLineValue ||
                existing.HomeTeamCurrentMoneyLineDisplayValue != incoming.HomeTeamCurrentMoneyLineDisplayValue ||
                existing.HomeTeamCurrentMoneyLineAlternateDisplayValue !=
                incoming.HomeTeamCurrentMoneyLineAlternateDisplayValue ||
                existing.HomeTeamCurrentMoneyLineDecimal != incoming.HomeTeamCurrentMoneyLineDecimal ||
                existing.HomeTeamCurrentMoneyLineFraction != incoming.HomeTeamCurrentMoneyLineFraction ||
                existing.HomeTeamCurrentMoneyLineAmerican != incoming.HomeTeamCurrentMoneyLineAmerican ||
                existing.HomeTeamCurrentMoneyLineOutcomeType != incoming.HomeTeamCurrentMoneyLineOutcomeType ||

                // Current Over/Under/Total
                existing.CurrentOverValue != incoming.CurrentOverValue ||
                existing.CurrentOverDisplayValue != incoming.CurrentOverDisplayValue ||
                existing.CurrentOverAlternateDisplayValue != incoming.CurrentOverAlternateDisplayValue ||
                existing.CurrentOverDecimal != incoming.CurrentOverDecimal ||
                existing.CurrentOverFraction != incoming.CurrentOverFraction ||
                existing.CurrentOverAmerican != incoming.CurrentOverAmerican ||
                existing.CurrentOverOutcomeType != incoming.CurrentOverOutcomeType ||

                existing.CurrentUnderValue != incoming.CurrentUnderValue ||
                existing.CurrentUnderDisplayValue != incoming.CurrentUnderDisplayValue ||
                existing.CurrentUnderAlternateDisplayValue != incoming.CurrentUnderAlternateDisplayValue ||
                existing.CurrentUnderDecimal != incoming.CurrentUnderDecimal ||
                existing.CurrentUnderFraction != incoming.CurrentUnderFraction ||
                existing.CurrentUnderAmerican != incoming.CurrentUnderAmerican ||
                existing.CurrentUnderOutcomeType != incoming.CurrentUnderOutcomeType ||

                existing.CurrentTotalValue != incoming.CurrentTotalValue ||
                existing.CurrentTotalDisplayValue != incoming.CurrentTotalDisplayValue ||
                existing.CurrentTotalAlternateDisplayValue != incoming.CurrentTotalAlternateDisplayValue ||
                existing.CurrentTotalDecimal != incoming.CurrentTotalDecimal ||
                existing.CurrentTotalFraction != incoming.CurrentTotalFraction ||
                existing.CurrentTotalAmerican != incoming.CurrentTotalAmerican ||

                // CloseTotal
                existing.CloseTotalValue != incoming.CloseTotalValue ||
                existing.CloseTotalDisplayValue != incoming.CloseTotalDisplayValue ||
                existing.CloseTotalAlternateDisplayValue != incoming.CloseTotalAlternateDisplayValue ||
                existing.CloseTotalDecimal != incoming.CloseTotalDecimal ||
                existing.CloseTotalFraction != incoming.CloseTotalFraction ||
                existing.CloseTotalAmerican != incoming.CloseTotalAmerican ||

                // Close Over
                existing.CloseOverValue != incoming.CloseOverValue ||
                existing.CloseOverDisplayValue != incoming.CloseOverDisplayValue ||
                existing.CloseOverAlternateDisplayValue != incoming.CloseOverAlternateDisplayValue ||
                existing.CloseOverDecimal != incoming.CloseOverDecimal ||
                existing.CloseOverFraction != incoming.CloseOverFraction ||
                existing.CloseOverAmerican != incoming.CloseOverAmerican ||

                // Close Under
                existing.CloseUnderValue != incoming.CloseUnderValue ||
                existing.CloseUnderDisplayValue != incoming.CloseUnderDisplayValue ||
                existing.CloseUnderAlternateDisplayValue != incoming.CloseUnderAlternateDisplayValue ||
                existing.CloseUnderDecimal != incoming.CloseUnderDecimal ||
                existing.CloseUnderFraction != incoming.CloseUnderFraction ||
                existing.CloseUnderAmerican != incoming.CloseUnderAmerican;
        }


        public static void UpdateFrom(this ContestOdds target, ContestOdds source)
        {
            if (target == null || source == null) return;

            target.ProviderId = source.ProviderId;
            target.ProviderName = source.ProviderName;
            target.ProviderPriority = source.ProviderPriority;
            target.Details = source.Details;
            target.OverUnder = source.OverUnder;
            target.Spread = source.Spread;
            target.OverOdds = source.OverOdds;
            target.UnderOdds = source.UnderOdds;
            target.MoneylineWinner = source.MoneylineWinner;
            target.SpreadWinner = source.SpreadWinner;

            target.HomeTeamFavorite = source.HomeTeamFavorite;
            target.HomeTeamUnderdog = source.HomeTeamUnderdog;
            target.HomeTeamMoneyLine = source.HomeTeamMoneyLine;
            target.HomeTeamSpreadOdds = source.HomeTeamSpreadOdds;

            target.AwayTeamFavorite = source.AwayTeamFavorite;
            target.AwayTeamUnderdog = source.AwayTeamUnderdog;
            target.AwayTeamMoneyLine = source.AwayTeamMoneyLine;
            target.AwayTeamSpreadOdds = source.AwayTeamSpreadOdds;

            target.OpenOverValue = source.OpenOverValue;
            target.OpenOverDisplayValue = source.OpenOverDisplayValue;
            target.OpenOverAlternateDisplayValue = source.OpenOverAlternateDisplayValue;
            target.OpenOverDecimal = source.OpenOverDecimal;
            target.OpenOverFraction = source.OpenOverFraction;
            target.OpenOverAmerican = source.OpenOverAmerican;

            target.OpenUnderValue = source.OpenUnderValue;
            target.OpenUnderDisplayValue = source.OpenUnderDisplayValue;
            target.OpenUnderAlternateDisplayValue = source.OpenUnderAlternateDisplayValue;
            target.OpenUnderDecimal = source.OpenUnderDecimal;
            target.OpenUnderFraction = source.OpenUnderFraction;
            target.OpenUnderAmerican = source.OpenUnderAmerican;

            target.OpenTotalValue = source.OpenTotalValue;
            target.OpenTotalDisplayValue = source.OpenTotalDisplayValue;
            target.OpenTotalAlternateDisplayValue = source.OpenTotalAlternateDisplayValue;
            target.OpenTotalDecimal = source.OpenTotalDecimal;
            target.OpenTotalFraction = source.OpenTotalFraction;
            target.OpenTotalAmerican = source.OpenTotalAmerican;

            // Away Team Open
            target.AwayTeamOpenFavorite = source.AwayTeamOpenFavorite;
            target.AwayTeamOpenPointSpreadAlternateDisplayValue = source.AwayTeamOpenPointSpreadAlternateDisplayValue;
            target.AwayTeamOpenPointSpreadAmerican = source.AwayTeamOpenPointSpreadAmerican;
            target.AwayTeamOpenSpreadValue = source.AwayTeamOpenSpreadValue;
            target.AwayTeamOpenSpreadDisplayValue = source.AwayTeamOpenSpreadDisplayValue;
            target.AwayTeamOpenSpreadAlternateDisplayValue = source.AwayTeamOpenSpreadAlternateDisplayValue;
            target.AwayTeamOpenSpreadDecimal = source.AwayTeamOpenSpreadDecimal;
            target.AwayTeamOpenSpreadFraction = source.AwayTeamOpenSpreadFraction;
            target.AwayTeamOpenSpreadAmerican = source.AwayTeamOpenSpreadAmerican;
            target.AwayTeamOpenMoneyLineValue = source.AwayTeamOpenMoneyLineValue;
            target.AwayTeamOpenMoneyLineDisplayValue = source.AwayTeamOpenMoneyLineDisplayValue;
            target.AwayTeamOpenMoneyLineAlternateDisplayValue = source.AwayTeamOpenMoneyLineAlternateDisplayValue;
            target.AwayTeamOpenMoneyLineDecimal = source.AwayTeamOpenMoneyLineDecimal;
            target.AwayTeamOpenMoneyLineFraction = source.AwayTeamOpenMoneyLineFraction;
            target.AwayTeamOpenMoneyLineAmerican = source.AwayTeamOpenMoneyLineAmerican;

            // Home Team Open
            target.HomeTeamOpenFavorite = source.HomeTeamOpenFavorite;
            target.HomeTeamOpenPointSpreadValue = source.HomeTeamOpenPointSpreadValue;
            target.HomeTeamOpenPointSpreadDisplayValue = source.HomeTeamOpenPointSpreadDisplayValue;
            target.HomeTeamOpenPointSpreadAlternateDisplayValue = source.HomeTeamOpenPointSpreadAlternateDisplayValue;
            target.HomeTeamOpenPointSpreadDecimal = source.HomeTeamOpenPointSpreadDecimal;
            target.HomeTeamOpenPointSpreadFraction = source.HomeTeamOpenPointSpreadFraction;
            target.HomeTeamOpenPointSpreadAmerican = source.HomeTeamOpenPointSpreadAmerican;
            target.HomeTeamOpenSpreadValue = source.HomeTeamOpenSpreadValue;
            target.HomeTeamOpenSpreadDisplayValue = source.HomeTeamOpenSpreadDisplayValue;
            target.HomeTeamOpenSpreadAlternateDisplayValue = source.HomeTeamOpenSpreadAlternateDisplayValue;
            target.HomeTeamOpenSpreadDecimal = source.HomeTeamOpenSpreadDecimal;
            target.HomeTeamOpenSpreadFraction = source.HomeTeamOpenSpreadFraction;
            target.HomeTeamOpenSpreadAmerican = source.HomeTeamOpenSpreadAmerican;
            target.HomeTeamOpenMoneyLineValue = source.HomeTeamOpenMoneyLineValue;
            target.HomeTeamOpenMoneyLineDisplayValue = source.HomeTeamOpenMoneyLineDisplayValue;
            target.HomeTeamOpenMoneyLineAlternateDisplayValue = source.HomeTeamOpenMoneyLineAlternateDisplayValue;
            target.HomeTeamOpenMoneyLineDecimal = source.HomeTeamOpenMoneyLineDecimal;
            target.HomeTeamOpenMoneyLineFraction = source.HomeTeamOpenMoneyLineFraction;
            target.HomeTeamOpenMoneyLineAmerican = source.HomeTeamOpenMoneyLineAmerican;

            // Away Team Close
            target.AwayTeamClosePointSpreadAlternateDisplayValue = source.AwayTeamClosePointSpreadAlternateDisplayValue;
            target.AwayTeamClosePointSpreadAmerican = source.AwayTeamClosePointSpreadAmerican;
            target.AwayTeamCloseSpreadValue = source.AwayTeamCloseSpreadValue;
            target.AwayTeamCloseSpreadDisplayValue = source.AwayTeamCloseSpreadDisplayValue;
            target.AwayTeamCloseSpreadAlternateDisplayValue = source.AwayTeamCloseSpreadAlternateDisplayValue;
            target.AwayTeamCloseSpreadDecimal = source.AwayTeamCloseSpreadDecimal;
            target.AwayTeamCloseSpreadFraction = source.AwayTeamCloseSpreadFraction;
            target.AwayTeamCloseSpreadAmerican = source.AwayTeamCloseSpreadAmerican;
            target.AwayTeamCloseMoneyLineValue = source.AwayTeamCloseMoneyLineValue;
            target.AwayTeamCloseMoneyLineDisplayValue = source.AwayTeamCloseMoneyLineDisplayValue;
            target.AwayTeamCloseMoneyLineAlternateDisplayValue = source.AwayTeamCloseMoneyLineAlternateDisplayValue;
            target.AwayTeamCloseMoneyLineDecimal = source.AwayTeamCloseMoneyLineDecimal;
            target.AwayTeamCloseMoneyLineFraction = source.AwayTeamCloseMoneyLineFraction;
            target.AwayTeamCloseMoneyLineAmerican = source.AwayTeamCloseMoneyLineAmerican;

            // Home Team Close
            target.HomeTeamClosePointSpreadAlternateDisplayValue = source.HomeTeamClosePointSpreadAlternateDisplayValue;
            target.HomeTeamClosePointSpreadAmerican = source.HomeTeamClosePointSpreadAmerican;
            target.HomeTeamCloseSpreadValue = source.HomeTeamCloseSpreadValue;
            target.HomeTeamCloseSpreadDisplayValue = source.HomeTeamCloseSpreadDisplayValue;
            target.HomeTeamCloseSpreadAlternateDisplayValue = source.HomeTeamCloseSpreadAlternateDisplayValue;
            target.HomeTeamCloseSpreadDecimal = source.HomeTeamCloseSpreadDecimal;
            target.HomeTeamCloseSpreadFraction = source.HomeTeamCloseSpreadFraction;
            target.HomeTeamCloseSpreadAmerican = source.HomeTeamCloseSpreadAmerican;
            target.HomeTeamCloseMoneyLineValue = source.HomeTeamCloseMoneyLineValue;
            target.HomeTeamCloseMoneyLineDisplayValue = source.HomeTeamCloseMoneyLineDisplayValue;
            target.HomeTeamCloseMoneyLineAlternateDisplayValue = source.HomeTeamCloseMoneyLineAlternateDisplayValue;
            target.HomeTeamCloseMoneyLineDecimal = source.HomeTeamCloseMoneyLineDecimal;
            target.HomeTeamCloseMoneyLineFraction = source.HomeTeamCloseMoneyLineFraction;
            target.HomeTeamCloseMoneyLineAmerican = source.HomeTeamCloseMoneyLineAmerican;

            // Away Team Current
            target.AwayTeamCurrentPointSpreadAlternateDisplayValue = source.AwayTeamCurrentPointSpreadAlternateDisplayValue;
            target.AwayTeamCurrentPointSpreadAmerican = source.AwayTeamCurrentPointSpreadAmerican;
            target.AwayTeamCurrentSpreadValue = source.AwayTeamCurrentSpreadValue;
            target.AwayTeamCurrentSpreadDisplayValue = source.AwayTeamCurrentSpreadDisplayValue;
            target.AwayTeamCurrentSpreadAlternateDisplayValue = source.AwayTeamCurrentSpreadAlternateDisplayValue;
            target.AwayTeamCurrentSpreadDecimal = source.AwayTeamCurrentSpreadDecimal;
            target.AwayTeamCurrentSpreadFraction = source.AwayTeamCurrentSpreadFraction;
            target.AwayTeamCurrentSpreadAmerican = source.AwayTeamCurrentSpreadAmerican;
            target.AwayTeamCurrentSpreadOutcomeType = source.AwayTeamCurrentSpreadOutcomeType;
            target.AwayTeamCurrentMoneyLineValue = source.AwayTeamCurrentMoneyLineValue;
            target.AwayTeamCurrentMoneyLineDisplayValue = source.AwayTeamCurrentMoneyLineDisplayValue;
            target.AwayTeamCurrentMoneyLineAlternateDisplayValue = source.AwayTeamCurrentMoneyLineAlternateDisplayValue;
            target.AwayTeamCurrentMoneyLineDecimal = source.AwayTeamCurrentMoneyLineDecimal;
            target.AwayTeamCurrentMoneyLineFraction = source.AwayTeamCurrentMoneyLineFraction;
            target.AwayTeamCurrentMoneyLineAmerican = source.AwayTeamCurrentMoneyLineAmerican;
            target.AwayTeamCurrentMoneyLineOutcomeType = source.AwayTeamCurrentMoneyLineOutcomeType;

            // Home Team Current
            target.HomeTeamCurrentPointSpreadAlternateDisplayValue = source.HomeTeamCurrentPointSpreadAlternateDisplayValue;
            target.HomeTeamCurrentPointSpreadAmerican = source.HomeTeamCurrentPointSpreadAmerican;
            target.HomeTeamCurrentSpreadValue = source.HomeTeamCurrentSpreadValue;
            target.HomeTeamCurrentSpreadDisplayValue = source.HomeTeamCurrentSpreadDisplayValue;
            target.HomeTeamCurrentSpreadAlternateDisplayValue = source.HomeTeamCurrentSpreadAlternateDisplayValue;
            target.HomeTeamCurrentSpreadDecimal = source.HomeTeamCurrentSpreadDecimal;
            target.HomeTeamCurrentSpreadFraction = source.HomeTeamCurrentSpreadFraction;
            target.HomeTeamCurrentSpreadAmerican = source.HomeTeamCurrentSpreadAmerican;
            target.HomeTeamCurrentSpreadOutcomeType = source.HomeTeamCurrentSpreadOutcomeType;
            target.HomeTeamCurrentMoneyLineValue = source.HomeTeamCurrentMoneyLineValue;
            target.HomeTeamCurrentMoneyLineDisplayValue = source.HomeTeamCurrentMoneyLineDisplayValue;
            target.HomeTeamCurrentMoneyLineAlternateDisplayValue = source.HomeTeamCurrentMoneyLineAlternateDisplayValue;
            target.HomeTeamCurrentMoneyLineDecimal = source.HomeTeamCurrentMoneyLineDecimal;
            target.HomeTeamCurrentMoneyLineFraction = source.HomeTeamCurrentMoneyLineFraction;
            target.HomeTeamCurrentMoneyLineAmerican = source.HomeTeamCurrentMoneyLineAmerican;
            target.HomeTeamCurrentMoneyLineOutcomeType = source.HomeTeamCurrentMoneyLineOutcomeType;

            // Current Over/Under/Total
            target.CurrentOverValue = source.CurrentOverValue;
            target.CurrentOverDisplayValue = source.CurrentOverDisplayValue;
            target.CurrentOverAlternateDisplayValue = source.CurrentOverAlternateDisplayValue;
            target.CurrentOverDecimal = source.CurrentOverDecimal;
            target.CurrentOverFraction = source.CurrentOverFraction;
            target.CurrentOverAmerican = source.CurrentOverAmerican;
            target.CurrentOverOutcomeType = source.CurrentOverOutcomeType;

            target.CurrentUnderValue = source.CurrentUnderValue;
            target.CurrentUnderDisplayValue = source.CurrentUnderDisplayValue;
            target.CurrentUnderAlternateDisplayValue = source.CurrentUnderAlternateDisplayValue;
            target.CurrentUnderDecimal = source.CurrentUnderDecimal;
            target.CurrentUnderFraction = source.CurrentUnderFraction;
            target.CurrentUnderAmerican = source.CurrentUnderAmerican;
            target.CurrentUnderOutcomeType = source.CurrentUnderOutcomeType;

            target.CurrentTotalValue = source.CurrentTotalValue;
            target.CurrentTotalDisplayValue = source.CurrentTotalDisplayValue;
            target.CurrentTotalAlternateDisplayValue = source.CurrentTotalAlternateDisplayValue;
            target.CurrentTotalDecimal = source.CurrentTotalDecimal;
            target.CurrentTotalFraction = source.CurrentTotalFraction;
            target.CurrentTotalAmerican = source.CurrentTotalAmerican;

            // CloseTotal
            target.CloseTotalValue = source.CloseTotalValue;
            target.CloseTotalDisplayValue = source.CloseTotalDisplayValue;
            target.CloseTotalAlternateDisplayValue = source.CloseTotalAlternateDisplayValue;
            target.CloseTotalDecimal = source.CloseTotalDecimal;
            target.CloseTotalFraction = source.CloseTotalFraction;
            target.CloseTotalAmerican = source.CloseTotalAmerican;

            // Close Over
            target.CloseOverValue = source.CloseOverValue;
            target.CloseOverDisplayValue = source.CloseOverDisplayValue;
            target.CloseOverAlternateDisplayValue = source.CloseOverAlternateDisplayValue;
            target.CloseOverDecimal = source.CloseOverDecimal;
            target.CloseOverFraction = source.CloseOverFraction;
            target.CloseOverAmerican = source.CloseOverAmerican;

            // Close Under
            target.CloseUnderValue = source.CloseUnderValue;
            target.CloseUnderDisplayValue = source.CloseUnderDisplayValue;
            target.CloseUnderAlternateDisplayValue = source.CloseUnderAlternateDisplayValue;
            target.CloseUnderDecimal = source.CloseUnderDecimal;
            target.CloseUnderFraction = source.CloseUnderFraction;
            target.CloseUnderAmerican = source.CloseUnderAmerican;
        }

    }
}
