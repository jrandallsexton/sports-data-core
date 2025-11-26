using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// Junction table linking Articles to AthleteSeasons
    /// </summary>
    public class ArticleAthleteSeason : CanonicalEntityBase<Guid>
    {
        public Guid ArticleId { get; set; }

        public Article Article { get; set; } = null!;

        /// <summary>
        /// Foreign key to AthleteSeason in the Producer database
        /// </summary>
        public Guid AthleteSeasonId { get; set; }

        /// <summary>
        /// Optional: Order for display purposes (0-based)
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Optional: Mark one AthleteSeason as the "primary" subject of the article
        /// </summary>
        public bool IsPrimary { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<ArticleAthleteSeason>
        {
            public void Configure(EntityTypeBuilder<ArticleAthleteSeason> builder)
            {
                builder.ToTable(nameof(ArticleAthleteSeason));

                builder.HasKey(x => x.Id);

                // Composite unique index: Article -> AthleteSeason direction
                builder.HasIndex(x => new { x.ArticleId, x.AthleteSeasonId })
                    .IsUnique()
                    .HasDatabaseName("IX_ArticleAthleteSeason_ArticleId_AthleteSeasonId");

                // Additional index: AthleteSeason -> Article direction (for reverse lookups)
                builder.HasIndex(x => x.AthleteSeasonId)
                    .HasDatabaseName("IX_ArticleAthleteSeason_AthleteSeasonId");

                builder.Property(x => x.ArticleId)
                    .IsRequired();

                builder.Property(x => x.AthleteSeasonId)
                    .IsRequired();

                builder.Property(x => x.DisplayOrder)
                    .HasDefaultValue(0);

                builder.Property(x => x.IsPrimary)
                    .HasDefaultValue(false);

                builder.HasOne(x => x.Article)
                    .WithMany(x => x.AthleteSeasons)
                    .HasForeignKey(x => x.ArticleId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ArticleAthleteSeason_Article");

                // Note: No navigation to AthleteSeason entity since it's in a different database (Producer)
                // The AthleteSeasonId is just a Guid foreign key for querying
            }
        }
    }
}
