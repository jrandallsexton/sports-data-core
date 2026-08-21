using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

/// <summary>
/// One row of the league-wide season stat leaderboard: "{athlete} ranks
/// {rank} in {category} for {seasonYear}" — scoped by season TYPE, because
/// types/2 (regular season) and types/3 (through postseason) are verified
/// DISTINCT datasets with different totals and different leaders. Sourced
/// from ESPN's seasons/{year}/types/{n}/leaders document (the one behind
/// their Season Leaders UI); past seasons are frozen canonical data.
///
/// Rows are replaced WHOLESALE per (SeasonYear, SeasonTypeCode) on each
/// re-source — ranks shuffle with every game week, so per-row identity has
/// no value. Leaders whose athlete cannot be resolved to an AthleteSeason
/// are skipped (logged): the document carries refs, not names, so an
/// unresolved row has nothing for a consumer to join or display.
/// </summary>
public class SeasonTypeLeader : CanonicalEntityBase<Guid>
{
    public int SeasonYear { get; set; }

    /// <summary>ESPN season type: 2 = regular season, 3 = through postseason.</summary>
    public int SeasonTypeCode { get; set; }

    /// <summary>Category key from ESPN (e.g. "passingYards").</summary>
    public required string CategoryName { get; set; }

    /// <summary>Display name from ESPN (e.g. "Passing Yards").</summary>
    public string? CategoryDisplayName { get; set; }

    /// <summary>1-based position within the category (document order).</summary>
    public int Rank { get; set; }

    public decimal Value { get; set; }

    public string? DisplayValue { get; set; }

    public Guid AthleteSeasonId { get; set; }

    /// <summary>Null when the leader's team ref did not resolve (athlete still ranked).</summary>
    public Guid? FranchiseSeasonId { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonTypeLeader>
    {
        public void Configure(EntityTypeBuilder<SeasonTypeLeader> builder)
        {
            builder.ToTable(nameof(SeasonTypeLeader));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.CategoryName).HasMaxLength(64).IsRequired();
            builder.Property(x => x.CategoryDisplayName).HasMaxLength(128);
            builder.Property(x => x.DisplayValue).HasMaxLength(32);
            builder.Property(x => x.Value).HasPrecision(12, 4);

            // The replace-scope + the read shapes: "leaders for a season/type"
            // and "is this athlete a leader anywhere".
            builder.HasIndex(x => new { x.SeasonYear, x.SeasonTypeCode, x.CategoryName });
            builder.HasIndex(x => x.AthleteSeasonId);
        }
    }
}
