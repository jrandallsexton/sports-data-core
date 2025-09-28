using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

public class CompetitionTeamOdds : CanonicalEntityBase<Guid>
{
    public Guid CompetitionOddsId { get; set; }
    public CompetitionOdds CompetitionOdds { get; set; } = null!;

    /// <summary>"Home" | "Away"</summary>
    public required string Side { get; set; }

    public bool? IsFavorite { get; set; }
    public bool? IsUnderdog { get; set; }

    public Guid FranchiseSeasonId { get; set; }

    // Open / Current / Close fields you settled on (trimmed here for brevity)
    public int? MoneylineOpen { get; set; }
    public int? MoneylineCurrent { get; set; }
    public int? MoneylineClose { get; set; }

    public decimal? SpreadPointsOpen { get; set; }
    public decimal? SpreadPointsCurrent { get; set; }
    public decimal? SpreadPointsClose { get; set; }

    public decimal? SpreadPriceOpen { get; set; }
    public decimal? SpreadPriceCurrent { get; set; }
    public decimal? SpreadPriceClose { get; set; }

    public DateTime? ClosedUtc { get; set; }
    public DateTime? CorrectedUtc { get; set; }  // ← NEW

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionTeamOdds>
    {
        public void Configure(EntityTypeBuilder<CompetitionTeamOdds> builder)
        {
            builder.ToTable(nameof(CompetitionTeamOdds));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Side).IsRequired().HasMaxLength(16);
            builder.Property(x => x.FranchiseSeasonId).IsRequired();

            builder.HasOne(x => x.CompetitionOdds)
                   .WithMany(x => x.Teams)
                   .HasForeignKey(x => x.CompetitionOddsId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => new { x.CompetitionOddsId, x.Side }).IsUnique();

            foreach (var p in typeof(CompetitionTeamOdds).GetProperties()
                         .Where(p => p.PropertyType == typeof(decimal?)))
            {
                builder.Property(p.Name).HasPrecision(18, 6);
            }
        }
    }
}
