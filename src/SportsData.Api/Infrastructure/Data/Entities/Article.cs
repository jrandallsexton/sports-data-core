using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class Article : CanonicalEntityBase<Guid>
    {
        public required string Title { get; set; }

        /// <summary>
        /// Short summary/excerpt for article previews, lists, and meta descriptions.
        /// Should be 150-300 characters for optimal SEO and readability.
        /// </summary>
        public string? Summary { get; set; }

        public required string Content { get; set; }

        public Guid? ContestId { get; set; }

        public DateTime PublishedAt { get; set; }

        public Guid AuthorId { get; set; }

        public User Author { get; set; } = default!;

        public int? Tokens { get; set; }

        public long? TimeMs { get; set; }

        public string? AiModel { get; set; }

        public string? AiPromptNameAndVersion { get; set; }

        // Many-to-many relationships via junction tables
        public ICollection<ArticleFranchiseSeason> FranchiseSeasons { get; set; } = [];

        public ICollection<ArticleAthleteSeason> AthleteSeasons { get; set; } = [];

        public string[] ImageUrls { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Article>
        {
            public void Configure(EntityTypeBuilder<Article> builder)
            {
                builder.ToTable(nameof(Article));
                builder.HasKey(x => x.Id);
                
                builder.Property(x => x.Title)
                    .IsRequired()
                    .HasMaxLength(200);

                builder.Property(x => x.Summary)
                    .HasMaxLength(500); // Optional, max 500 chars for preview/SEO
                
                builder.Property(x => x.Content)
                    .IsRequired()
                    .HasColumnType("text"); // PostgreSQL text type - unlimited size, TOAST-compressed
                
                builder.Property(x => x.PublishedAt)
                    .IsRequired();
                
                builder.HasOne(x => x.Author)
                    .WithMany()
                    .HasForeignKey(x => x.AuthorId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Configure many-to-many relationships
                builder.HasMany(x => x.FranchiseSeasons)
                    .WithOne(x => x.Article)
                    .HasForeignKey(x => x.ArticleId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.AthleteSeasons)
                    .WithOne(x => x.Article)
                    .HasForeignKey(x => x.ArticleId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.Property(a => a.ImageUrls)
                    .HasColumnType("text[]"); // PostgreSQL array type
            }
        }
    }
}
