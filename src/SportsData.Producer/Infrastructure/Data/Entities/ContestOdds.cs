using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class ContestOdds : CanonicalEntityBase<Guid>
    {
        // TODO: Re-analyze my decision to flatten this entity instead of normalizing it.

        public Guid ContestId { get; set; }

        public required Uri ProviderRef { get; set; }

        public required string ProviderId { get; set; }

        public required string ProviderName { get; set; }

        public int ProviderPriority { get; set; }

        public string? Details { get; set; }

        public decimal? OverUnder { get; set; }

        public decimal? Spread { get; set; }

        public decimal? OverOdds { get; set; }

        public decimal? UnderOdds { get; set; }

        public bool MoneylineWinner { get; set; }

        public bool SpreadWinner { get; set; }

        // ***** Away Team Odds *****

        public bool AwayTeamFavorite { get; set; }

        public bool AwayTeamUnderdog { get; set; }

        public int? AwayTeamMoneyLine { get; set; }

        public decimal? AwayTeamSpreadOdds { get; set; }

        public bool AwayTeamOpenFavorite { get; set; }

        public string? AwayTeamOpenPointSpreadAlternateDisplayValue { get; set; }

        public string? AwayTeamOpenPointSpreadAmerican { get; set; }

        public decimal? AwayTeamOpenSpreadValue { get; set; }

        public string? AwayTeamOpenSpreadDisplayValue { get; set; }

        public string? AwayTeamOpenSpreadAlternateDisplayValue { get; set; }

        public decimal? AwayTeamOpenSpreadDecimal { get; set; }

        public string? AwayTeamOpenSpreadFraction { get; set; }

        public string? AwayTeamOpenSpreadAmerican { get; set; }

        public decimal? AwayTeamOpenMoneyLineValue { get; set; }

        public string? AwayTeamOpenMoneyLineDisplayValue { get; set; }

        public string? AwayTeamOpenMoneyLineAlternateDisplayValue { get; set; }

        public decimal? AwayTeamOpenMoneyLineDecimal { get; set; }

        public string? AwayTeamOpenMoneyLineFraction { get; set; }

        public string? AwayTeamOpenMoneyLineAmerican { get; set; }

        public string? AwayTeamClosePointSpreadAlternateDisplayValue { get; set; }

        public string? AwayTeamClosePointSpreadAmerican { get; set; }

        public decimal? AwayTeamCloseSpreadValue { get; set; }

        public string? AwayTeamCloseSpreadDisplayValue { get; set; }

        public string? AwayTeamCloseSpreadAlternateDisplayValue { get; set; }

        public decimal? AwayTeamCloseSpreadDecimal { get; set; }

        public string? AwayTeamCloseSpreadFraction { get; set; }

        public string? AwayTeamCloseSpreadAmerican { get; set; }

        public decimal? AwayTeamCloseMoneyLineValue { get; set; }

        public string? AwayTeamCloseMoneyLineDisplayValue { get; set; }

        public string? AwayTeamCloseMoneyLineAlternateDisplayValue { get; set; }

        public decimal? AwayTeamCloseMoneyLineDecimal { get; set; }

        public string? AwayTeamCloseMoneyLineFraction { get; set; }

        public string? AwayTeamCloseMoneyLineAmerican { get; set; }

        public string? AwayTeamCurrentPointSpreadAlternateDisplayValue { get; set; }

        public string? AwayTeamCurrentPointSpreadAmerican { get; set; }

        public decimal? AwayTeamCurrentSpreadValue { get; set; }

        public string? AwayTeamCurrentSpreadDisplayValue { get; set; }

        public string? AwayTeamCurrentSpreadAlternateDisplayValue { get; set; }

        public decimal? AwayTeamCurrentSpreadDecimal { get; set; }

        public string? AwayTeamCurrentSpreadFraction { get; set; }

        public string? AwayTeamCurrentSpreadAmerican { get; set; }

        public string? AwayTeamCurrentSpreadOutcomeType { get; set; }

        public decimal? AwayTeamCurrentMoneyLineValue { get; set; }

        public string? AwayTeamCurrentMoneyLineDisplayValue { get; set; }

        public string? AwayTeamCurrentMoneyLineAlternateDisplayValue { get; set; }

        public decimal? AwayTeamCurrentMoneyLineDecimal { get; set; }

        public string? AwayTeamCurrentMoneyLineFraction { get; set; }

        public string? AwayTeamCurrentMoneyLineAmerican { get; set; }

        public string? AwayTeamCurrentMoneyLineOutcomeType { get; set; }

        public Guid AwayTeamFranchiseSeasonId { get; set; }

        // ***** Home Team Odds *****
        public bool HomeTeamFavorite { get; set; }

        public bool HomeTeamUnderdog { get; set; }

        public int? HomeTeamMoneyLine { get; set; }

        public decimal? HomeTeamSpreadOdds { get; set; }

        public bool HomeTeamOpenFavorite { get; set; }

        public decimal? HomeTeamOpenPointSpreadValue { get; set; }

        public string? HomeTeamOpenPointSpreadDisplayValue { get; set; }

        public string? HomeTeamOpenPointSpreadAlternateDisplayValue { get; set; }

        public decimal? HomeTeamOpenPointSpreadDecimal { get; set; }

        public string? HomeTeamOpenPointSpreadFraction { get; set; }

        public string? HomeTeamOpenPointSpreadAmerican { get; set; }

        public decimal? HomeTeamOpenSpreadValue { get; set; }

        public string? HomeTeamOpenSpreadDisplayValue { get; set; }

        public string? HomeTeamOpenSpreadAlternateDisplayValue { get; set; }

        public decimal? HomeTeamOpenSpreadDecimal { get; set; }

        public string? HomeTeamOpenSpreadFraction { get; set; }

        public string? HomeTeamOpenSpreadAmerican { get; set; }

        public decimal? HomeTeamOpenMoneyLineValue { get; set; }

        public string? HomeTeamOpenMoneyLineDisplayValue { get; set; }

        public string? HomeTeamOpenMoneyLineAlternateDisplayValue { get; set; }

        public decimal? HomeTeamOpenMoneyLineDecimal { get; set; }

        public string? HomeTeamOpenMoneyLineFraction { get; set; }

        public string? HomeTeamOpenMoneyLineAmerican { get; set; }

        public string? HomeTeamClosePointSpreadAlternateDisplayValue { get; set; }

        public string? HomeTeamClosePointSpreadAmerican { get; set; }

        public decimal? HomeTeamCloseSpreadValue { get; set; }

        public string? HomeTeamCloseSpreadDisplayValue { get; set; }

        public string? HomeTeamCloseSpreadAlternateDisplayValue { get; set; }

        public decimal? HomeTeamCloseSpreadDecimal { get; set; }

        public string? HomeTeamCloseSpreadFraction { get; set; }

        public string? HomeTeamCloseSpreadAmerican { get; set; }

        public decimal? HomeTeamCloseMoneyLineValue { get; set; }

        public string? HomeTeamCloseMoneyLineDisplayValue { get; set; }

        public string? HomeTeamCloseMoneyLineAlternateDisplayValue { get; set; }

        public decimal? HomeTeamCloseMoneyLineDecimal { get; set; }

        public string? HomeTeamCloseMoneyLineFraction { get; set; }

        public string? HomeTeamCloseMoneyLineAmerican { get; set; }

        public string? HomeTeamCurrentPointSpreadAlternateDisplayValue { get; set; }

        public string? HomeTeamCurrentPointSpreadAmerican { get; set; }

        public decimal? HomeTeamCurrentSpreadValue { get; set; }

        public string? HomeTeamCurrentSpreadDisplayValue { get; set; }

        public string? HomeTeamCurrentSpreadAlternateDisplayValue { get; set; }

        public decimal? HomeTeamCurrentSpreadDecimal { get; set; }

        public string? HomeTeamCurrentSpreadFraction { get; set; }

        public string? HomeTeamCurrentSpreadAmerican { get; set; }

        public string? HomeTeamCurrentSpreadOutcomeType { get; set; }

        public decimal? HomeTeamCurrentMoneyLineValue { get; set; }

        public string? HomeTeamCurrentMoneyLineDisplayValue { get; set; }

        public string? HomeTeamCurrentMoneyLineAlternateDisplayValue { get; set; }

        public decimal? HomeTeamCurrentMoneyLineDecimal { get; set; }

        public string? HomeTeamCurrentMoneyLineFraction { get; set; }

        public string? HomeTeamCurrentMoneyLineAmerican { get; set; }

        public string? HomeTeamCurrentMoneyLineOutcomeType { get; set; }

        public Guid HomeTeamFranchiseSeasonId { get; set; }

        // ***** Open Odds *****

        public decimal? OpenOverValue { get; set; }

        public string? OpenOverDisplayValue { get; set; }

        public string? OpenOverAlternateDisplayValue { get; set; }

        public decimal? OpenOverDecimal { get; set; }

        public string? OpenOverFraction { get; set; }

        public string? OpenOverAmerican { get; set; }

        public decimal? OpenUnderValue { get; set; }

        public string? OpenUnderDisplayValue { get; set; }

        public string? OpenUnderAlternateDisplayValue { get; set; }

        public decimal? OpenUnderDecimal { get; set; }

        public string? OpenUnderFraction { get; set; }

        public string? OpenUnderAmerican { get; set; }

        public decimal? OpenTotalValue { get; set; }

        public string? OpenTotalDisplayValue { get; set; }

        public string? OpenTotalAlternateDisplayValue { get; set; }

        public decimal? OpenTotalDecimal { get; set; }

        public string? OpenTotalFraction { get; set; }

        public string? OpenTotalAmerican { get; set; }

        // Close Odds

        public decimal? CloseOverValue { get; set; }

        public string? CloseOverDisplayValue { get; set; }

        public string? CloseOverAlternateDisplayValue { get; set; }

        public decimal? CloseOverDecimal { get; set; }

        public string? CloseOverFraction { get; set; }

        public string? CloseOverAmerican { get; set; }

        public decimal? CloseUnderValue { get; set; }

        public string? CloseUnderDisplayValue { get; set; }

        public string? CloseUnderAlternateDisplayValue { get; set; }

        public decimal? CloseUnderDecimal { get; set; }

        public string? CloseUnderFraction { get; set; }

        public string? CloseUnderAmerican { get; set; }

        public string? CloseTotalAlternateDisplayValue { get; set; }

        public string? CloseTotalAmerican { get; set; }

        public decimal? CloseTotalValue { get; set; }

        public string? CloseTotalDisplayValue { get; set; }

        public decimal? CloseTotalDecimal { get; set; }

        public string? CloseTotalFraction { get; set; }

        // Current Odds

        public decimal? CurrentOverValue { get; set; }

        public string? CurrentOverDisplayValue { get; set; }

        public string? CurrentOverAlternateDisplayValue { get; set; }

        public decimal? CurrentOverDecimal { get; set; }

        public string? CurrentOverFraction { get; set; }

        public string? CurrentOverAmerican { get; set; }

        public string? CurrentOverOutcomeType { get; set; }

        public decimal? CurrentUnderValue { get; set; }

        public string? CurrentUnderDisplayValue { get; set; }

        public string? CurrentUnderAlternateDisplayValue { get; set; }

        public decimal? CurrentUnderDecimal { get; set; }

        public string? CurrentUnderFraction { get; set; }

        public string? CurrentUnderAmerican { get; set; }

        public string? CurrentUnderOutcomeType { get; set; }

        public string? CurrentTotalAlternateDisplayValue { get; set; }

        public string? CurrentTotalAmerican { get; set; }

        public decimal? CurrentTotalValue { get; set; }

        public string? CurrentTotalDisplayValue { get; set; }

        public decimal? CurrentTotalDecimal { get; set; }
        public string? CurrentTotalFraction { get; set; }


        public class EntityConfiguration : IEntityTypeConfiguration<ContestOdds>
        {
            public void Configure(EntityTypeBuilder<ContestOdds> builder)
            {
                builder.ToTable(nameof(ContestOdds));
                builder.HasKey(x => x.Id);

                builder.HasOne<Contest>()
                    .WithMany(x => x.Odds)
                    .HasForeignKey(x => x.ContestId);

                builder.Property(x => x.ProviderRef)
                       .IsRequired()
                       .HasMaxLength(256);

                builder.Property(x => x.ProviderId)
                       .IsRequired()
                       .HasMaxLength(128);

                builder.Property(x => x.ProviderName)
                       .IsRequired()
                       .HasMaxLength(128);

                builder.Property(x => x.AwayTeamFranchiseSeasonId)
                    .IsRequired();

                builder.Property(x => x.HomeTeamFranchiseSeasonId)
                       .IsRequired();

                builder.Property(x => x.Details).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenPointSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenPointSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenSpreadDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenSpreadFraction).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenMoneyLineDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenMoneyLineAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenMoneyLineFraction).HasMaxLength(256);
                builder.Property(x => x.AwayTeamOpenMoneyLineAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamClosePointSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamClosePointSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCloseSpreadDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCloseSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCloseSpreadFraction).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCloseSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCloseMoneyLineDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCloseMoneyLineAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCloseMoneyLineFraction).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCloseMoneyLineAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentPointSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentPointSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentSpreadDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentSpreadFraction).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentSpreadOutcomeType).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentMoneyLineDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentMoneyLineAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentMoneyLineFraction).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentMoneyLineAmerican).HasMaxLength(256);
                builder.Property(x => x.AwayTeamCurrentMoneyLineOutcomeType).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenPointSpreadDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenPointSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenPointSpreadFraction).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenPointSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenSpreadDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenSpreadFraction).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenMoneyLineDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenMoneyLineAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenMoneyLineFraction).HasMaxLength(256);
                builder.Property(x => x.HomeTeamOpenMoneyLineAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamClosePointSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamClosePointSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCloseSpreadDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCloseSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCloseSpreadFraction).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCloseSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCloseMoneyLineDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCloseMoneyLineAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCloseMoneyLineFraction).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCloseMoneyLineAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentPointSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentPointSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentSpreadDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentSpreadAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentSpreadFraction).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentSpreadAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentSpreadOutcomeType).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentMoneyLineDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentMoneyLineAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentMoneyLineFraction).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentMoneyLineAmerican).HasMaxLength(256);
                builder.Property(x => x.HomeTeamCurrentMoneyLineOutcomeType).HasMaxLength(256);
                builder.Property(x => x.OpenOverDisplayValue).HasMaxLength(256);
                builder.Property(x => x.OpenOverAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.OpenOverFraction).HasMaxLength(256);
                builder.Property(x => x.OpenOverAmerican).HasMaxLength(256);
                builder.Property(x => x.OpenUnderDisplayValue).HasMaxLength(256);
                builder.Property(x => x.OpenUnderAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.OpenUnderFraction).HasMaxLength(256);
                builder.Property(x => x.OpenUnderAmerican).HasMaxLength(256);
                builder.Property(x => x.OpenTotalDisplayValue).HasMaxLength(256);
                builder.Property(x => x.OpenTotalAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.OpenTotalFraction).HasMaxLength(256);
                builder.Property(x => x.OpenTotalAmerican).HasMaxLength(256);
                builder.Property(x => x.CloseOverDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CloseOverAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CloseOverFraction).HasMaxLength(256);
                builder.Property(x => x.CloseOverAmerican).HasMaxLength(256);
                builder.Property(x => x.CloseUnderDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CloseUnderAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CloseUnderFraction).HasMaxLength(256);
                builder.Property(x => x.CloseUnderAmerican).HasMaxLength(256);
                builder.Property(x => x.CloseTotalAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CloseTotalAmerican).HasMaxLength(256);
                builder.Property(x => x.CurrentOverDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CurrentOverAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CurrentOverFraction).HasMaxLength(256);
                builder.Property(x => x.CurrentOverAmerican).HasMaxLength(256);
                builder.Property(x => x.CurrentOverOutcomeType).HasMaxLength(256);
                builder.Property(x => x.CurrentUnderDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CurrentUnderAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CurrentUnderFraction).HasMaxLength(256);
                builder.Property(x => x.CurrentUnderAmerican).HasMaxLength(256);
                builder.Property(x => x.CurrentUnderOutcomeType).HasMaxLength(256);
                builder.Property(x => x.CurrentTotalAlternateDisplayValue).HasMaxLength(256);
                builder.Property(x => x.CurrentTotalAmerican).HasMaxLength(256);
                builder.Property(x => x.CurrentTotalDisplayValue).HasMaxLength(256);

                foreach (var decimalProp in typeof(ContestOdds).GetProperties()
                    .Where(p => p.PropertyType == typeof(decimal?)))
                {
                    builder.Property(decimalProp.Name)
                           .HasPrecision(18, 6);
                }
            }
        }

    }
}
