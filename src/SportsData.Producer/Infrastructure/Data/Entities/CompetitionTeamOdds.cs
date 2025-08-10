using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionTeamOdds : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionOddsId { get; set; }

        /// <summary>
        /// 0 = Home, 1 = Away
        /// </summary>
        public required string Side { get; set; }

        public bool? IsFavorite { get; set; }
        public bool? IsUnderdog { get; set; }

        /// <summary>
        /// The main money line for this team (e.g., -110).
        /// </summary>
        public int? HeadlineMoneyLine { get; set; }

        /// <summary>
        /// The main spread odds for this team (e.g., -110).
        /// </summary>
        public decimal? HeadlineSpreadOdds { get; set; }

        /// <summary>
        /// Link to the team’s season in canonical data.
        /// </summary>
        public Guid FranchiseSeasonId { get; set; }

        // Navigation
        public CompetitionOdds CompetitionOdds { get; set; } = null!;
        public ICollection<CompetitionTeamOddsSnapshot> Snapshots { get; set; } = new List<CompetitionTeamOddsSnapshot>();

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionTeamOdds>
        {
            public void Configure(EntityTypeBuilder<CompetitionTeamOdds> builder)
            {
                builder.ToTable("CompetitionTeamOdds");

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Side)
                    .IsRequired()
                    .HasMaxLength(16);

                builder.Property(x => x.IsFavorite);
                builder.Property(x => x.IsUnderdog);

                builder.Property(x => x.HeadlineMoneyLine);
                builder.Property(x => x.HeadlineSpreadOdds)
                    .HasPrecision(18, 6);

                builder.Property(x => x.FranchiseSeasonId)
                    .IsRequired();

                builder.HasOne(x => x.CompetitionOdds)
                    .WithMany(x => x.Teams)
                    .HasForeignKey(x => x.CompetitionOddsId)
                    .OnDelete(DeleteBehavior.Cascade);

                //builder.HasMany(x => x.Snapshots)
                //    .WithOne(x => x.TeamOddsId)
                //    .HasForeignKey(x => x.TeamOddsId)
                //    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}