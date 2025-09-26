using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionOddsLink : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionOddsId { get; set; } // FK -> CompetitionOdds

        // ESPN has an array of rels; we store the raw joined string for fidelity.
        // Example: "home,desktop,bets,espn-bet"
        public required string Rel { get; set; }

        public string? Language { get; set; }      // e.g., "en-US"
        public required string Href { get; set; }  // full deeplink
        public string? Text { get; set; }          // "Home Bet"
        public string? ShortText { get; set; }     // "Home Bet"
        public bool IsExternal { get; set; }
        public bool IsPremium { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionOddsLink>
        {
            public void Configure(EntityTypeBuilder<CompetitionOddsLink> builder)
            {
                builder.ToTable(nameof(CompetitionOddsLink));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Rel).IsRequired().HasMaxLength(256);
                builder.Property(x => x.Language).HasMaxLength(32);
                builder.Property(x => x.Href).IsRequired().HasMaxLength(1024);
                builder.Property(x => x.Text).HasMaxLength(256);
                builder.Property(x => x.ShortText).HasMaxLength(256);

                builder.HasIndex(x => new { x.CompetitionOddsId, x.Rel, x.Href });

                builder.HasOne<CompetitionOdds>()
                    .WithMany(x => x.Links)
                    .HasForeignKey(x => x.CompetitionOddsId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}