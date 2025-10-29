using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionMedia : CanonicalEntityBase<Guid>
    {
        public Competition Competition { get; set; } = null!;
        public Guid CompetitionId { get; set; }

        public Guid AwayFranchiseSeasonId { get; set; }
        public FranchiseSeason? AwayFranchiseSeason { get; set; }

        public Guid HomeFranchiseSeasonId { get; set; }
        public FranchiseSeason? HomeFranchiseSeason { get; set; }

        public string VideoId { get; set; } = null!;
        public string ChannelId { get; set; } = null!;
        public string ChannelTitle { get; set; } = null!;

        public string Title { get; set; } = null!;
        public string Description { get; set; } = null!;

        public DateTime PublishedUtc { get; set; }

        public string ThumbnailDefaultUrl { get; set; } = null!;
        public int ThumbnailDefaultWidth { get; set; }
        public int ThumbnailDefaultHeight { get; set; }

        public string ThumbnailMediumUrl { get; set; } = null!;
        public int ThumbnailMediumWidth { get; set; }
        public int ThumbnailMediumHeight { get; set; }

        public string ThumbnailHighUrl { get; set; } = null!;
        public int ThumbnailHighWidth { get; set; }
        public int ThumbnailHighHeight { get; set; }

        public string Source => "YouTube"; // optional discriminator, in case you add other platforms

        public bool IsAdminPinned { get; set; } = false; // for future curation
        public bool IsAutoIndexed { get; set; } = true;  // whether AI/manual added

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionMedia>
        {
            public void Configure(EntityTypeBuilder<CompetitionMedia> builder)
            {
                builder.HasKey(x => x.Id);

                builder.HasIndex(x => x.CompetitionId);

                builder
                    .HasOne(x => x.Competition)
                    .WithMany(x => x.Media)
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.Property(x => x.VideoId).HasMaxLength(64);
                builder.Property(x => x.ChannelId).HasMaxLength(64);
                builder.Property(x => x.ChannelTitle).HasMaxLength(256);
                builder.Property(x => x.Title).HasMaxLength(512);
                builder.Property(x => x.Description).HasMaxLength(2048);

                builder.Property(x => x.ThumbnailDefaultUrl).HasMaxLength(512);
                builder.Property(x => x.ThumbnailMediumUrl).HasMaxLength(512);
                builder.Property(x => x.ThumbnailHighUrl).HasMaxLength(512);
            }
        }

    }
}