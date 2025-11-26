using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// Junction table linking Articles to FranchiseSeasons
    /// </summary>
    public class ArticleFranchiseSeason : CanonicalEntityBase<Guid>
    {
        public Guid ArticleId { get; set; }

        public Article Article { get; set; } = null!;

        /// <summary>
        /// Foreign key to FranchiseSeason in the Producer database
        /// </summary>
        public Guid FranchiseSeasonId { get; set; }

        /// <summary>
        /// Optional: Order for display purposes (0-based)
        /// </summary>
        public int DisplayOrder { get; set; }

        /// <summary>
        /// Optional: Mark one FranchiseSeason as the "primary" subject of the article
        /// </summary>
        public bool IsPrimary { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<ArticleFranchiseSeason>
        {
            public void Configure(EntityTypeBuilder<ArticleFranchiseSeason> builder)
            {
                builder.ToTable(nameof(ArticleFranchiseSeason));

                builder.HasKey(x => x.Id);

                // Composite unique index: Article -> FranchiseSeason direction
                builder.HasIndex(x => new { x.ArticleId, x.FranchiseSeasonId })
                    .IsUnique()
                    .HasDatabaseName("IX_ArticleFranchiseSeason_ArticleId_FranchiseSeasonId");

                // Additional index: FranchiseSeason -> Article direction (for reverse lookups)
                builder.HasIndex(x => x.FranchiseSeasonId)
                    .HasDatabaseName("IX_ArticleFranchiseSeason_FranchiseSeasonId");

                builder.Property(x => x.ArticleId)
                    .IsRequired();

                builder.Property(x => x.FranchiseSeasonId)
                    .IsRequired();

                builder.Property(x => x.DisplayOrder)
                    .HasDefaultValue(0);

                builder.Property(x => x.IsPrimary)
                    .HasDefaultValue(false);

                builder.HasOne(x => x.Article)
                    .WithMany(x => x.FranchiseSeasons)
                    .HasForeignKey(x => x.ArticleId)
                    .OnDelete(DeleteBehavior.Cascade)
                    .HasConstraintName("FK_ArticleFranchiseSeason_Article");

                // Note: No navigation to FranchiseSeason entity since it's in a different database (Producer)
                // The FranchiseSeasonId is just a Guid foreign key for querying
            }
        }
    }
}
