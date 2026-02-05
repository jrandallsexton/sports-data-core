using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CoachSeasonRecordStat : CanonicalEntityBase<Guid>
    {
        public required Guid CoachSeasonRecordId { get; set; }

        public CoachSeasonRecord CoachSeasonRecord { get; set; } = null!;

        public required string Name { get; set; }

        public string? DisplayName { get; set; }

        public string? ShortDisplayName { get; set; }

        public string? Description { get; set; }

        public string? Abbreviation { get; set; }

        public string? Type { get; set; }

        public double? Value { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CoachSeasonRecordStat>
        {
            public void Configure(EntityTypeBuilder<CoachSeasonRecordStat> builder)
            {
                builder.ToTable(nameof(CoachSeasonRecordStat));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(256);

                builder.Property(x => x.DisplayName)
                    .HasMaxLength(256);

                builder.Property(x => x.ShortDisplayName)
                    .HasMaxLength(256);

                builder.Property(x => x.Description)
                    .HasMaxLength(512);

                builder.Property(x => x.Abbreviation)
                    .HasMaxLength(64);

                builder.Property(x => x.Type)
                    .HasMaxLength(256);

                builder.HasOne(x => x.CoachSeasonRecord)
                    .WithMany(r => r.Stats)
                    .HasForeignKey(x => x.CoachSeasonRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
