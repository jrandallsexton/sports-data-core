using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionTeamOdds : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionOddsId { get; set; }
        public CompetitionOdds CompetitionOdds { get; set; } = null!;

        /// <summary> "Home" | "Away" </summary>
        public required string Side { get; set; }

        public bool? IsFavorite { get; set; }     // current headline
        public bool? IsUnderdog { get; set; }     // current headline

        /// <summary>The main (current) moneyline for this team (e.g., -110 or +195).</summary>
        public int? HeadlineMoneyLine { get; set; }

        /// <summary>The main (current) spread price for this team (e.g., -110).</summary>
        public decimal? HeadlineSpreadOdds { get; set; }

        /// <summary>Canonical link to the team’s season.</summary>
        public Guid FranchiseSeasonId { get; set; }

        public ICollection<CompetitionTeamOddsSnapshot> Snapshots { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionTeamOdds>
        {
            public void Configure(EntityTypeBuilder<CompetitionTeamOdds> builder)
            {
                builder.ToTable(nameof(CompetitionTeamOdds));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Side).IsRequired().HasMaxLength(16);
                builder.Property(x => x.HeadlineSpreadOdds).HasPrecision(18, 6);

                builder.Property(x => x.FranchiseSeasonId).IsRequired();

                builder.HasOne(x => x.CompetitionOdds)
                       .WithMany(x => x.Teams)
                       .HasForeignKey(x => x.CompetitionOddsId)
                       .OnDelete(DeleteBehavior.Cascade);

                // enforce a single row per provider/competition per side
                builder.HasIndex(x => new { x.CompetitionOddsId, x.Side }).IsUnique();
            }
        }
    }
}
