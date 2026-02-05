using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CoachSeasonRecord : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public required Guid CoachSeasonId { get; set; }

        public CoachSeason CoachSeason { get; set; } = null!;

        public required string Name { get; set; }

        public required string Type { get; set; }

        public string? Summary { get; set; }

        public string? DisplayValue { get; set; }

        public double? Value { get; set; }

        public ICollection<CoachSeasonRecordStat> Stats { get; set; } = [];

        public ICollection<CoachSeasonRecordExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CoachSeasonRecord>
        {
            public void Configure(EntityTypeBuilder<CoachSeasonRecord> builder)
            {
                builder.ToTable(nameof(CoachSeasonRecord));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(256);

                builder.Property(x => x.Type)
                    .IsRequired()
                    .HasMaxLength(256);

                builder.Property(x => x.Summary)
                    .HasMaxLength(512);

                builder.Property(x => x.DisplayValue)
                    .HasMaxLength(256);

                builder.HasOne(x => x.CoachSeason)
                    .WithMany()
                    .HasForeignKey(x => x.CoachSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Stats)
                    .WithOne(s => s.CoachSeasonRecord)
                    .HasForeignKey(s => s.CoachSeasonRecordId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.ExternalIds)
                    .WithOne(e => e.Record)
                    .HasForeignKey(e => e.CoachSeasonRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
