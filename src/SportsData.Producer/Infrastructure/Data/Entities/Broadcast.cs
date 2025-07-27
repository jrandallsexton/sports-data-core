using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class Broadcast : CanonicalEntityBase<Guid>
{
    public Competition Competition { get; set; } = null!;
    public Guid CompetitionId { get; set; }

    public string TypeId { get; set; } = default!;
    public string TypeShortName { get; set; } = default!;
    public string TypeLongName { get; set; } = default!;
    public string TypeSlug { get; set; } = default!;

    public int Channel { get; set; }

    public string? Station { get; set; }
    public string? StationKey { get; set; }
    public string? Url { get; set; }

    public string Slug { get; set; } = default!;
    public int Priority { get; set; }

    public string? MarketId { get; set; }
    public string? MarketType { get; set; }

    public string? MediaId { get; set; }
    public string? MediaCallLetters { get; set; }
    public string? MediaName { get; set; }
    public string? MediaShortName { get; set; }
    public string? MediaSlug { get; set; }

    public string? MediaGroupId { get; set; }
    public string? MediaGroupName { get; set; }
    public string? MediaGroupSlug { get; set; }

    public string? Language { get; set; }
    public string? Region { get; set; }

    public bool Partnered { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<Broadcast>
    {
        public void Configure(EntityTypeBuilder<Broadcast> builder)
        {
            builder.ToTable(nameof(Broadcast));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.TypeId).HasMaxLength(10).IsRequired();
            builder.Property(x => x.TypeShortName).HasMaxLength(50).IsRequired();
            builder.Property(x => x.TypeLongName).HasMaxLength(100).IsRequired();
            builder.Property(x => x.TypeSlug).HasMaxLength(50).IsRequired();

            builder.Property(x => x.Channel).IsRequired();
            builder.Property(x => x.Station).HasMaxLength(100);
            builder.Property(x => x.StationKey).HasMaxLength(50);
            builder.Property(x => x.Url).HasMaxLength(250);
            builder.Property(x => x.Slug).HasMaxLength(50).IsRequired();
            builder.Property(x => x.Priority).IsRequired();

            builder.Property(x => x.MarketId).HasMaxLength(10);
            builder.Property(x => x.MarketType).HasMaxLength(50);
            builder.Property(x => x.MediaId).HasMaxLength(10);
            builder.Property(x => x.MediaCallLetters).HasMaxLength(50);
            builder.Property(x => x.MediaName).HasMaxLength(100);
            builder.Property(x => x.MediaShortName).HasMaxLength(50);
            builder.Property(x => x.MediaSlug).HasMaxLength(50);
            builder.Property(x => x.MediaGroupId).HasMaxLength(10);
            builder.Property(x => x.MediaGroupName).HasMaxLength(100);
            builder.Property(x => x.MediaGroupSlug).HasMaxLength(50);
            builder.Property(x => x.Language).HasMaxLength(10);
            builder.Property(x => x.Region).HasMaxLength(10);

            builder.Property(x => x.Partnered).IsRequired();

            builder.HasOne<Competition>()
                .WithMany(x => x.Broadcasts)
                .HasForeignKey(x => x.CompetitionId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}