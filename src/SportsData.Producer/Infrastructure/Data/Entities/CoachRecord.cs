using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CoachRecord : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public required Guid CoachId { get; set; }

        public Coach Coach { get; set; } = null!;

        public required string Name { get; set; }

        public required string Type { get; set; }

        public string? Summary { get; set; }

        public string? DisplayValue { get; set; }

        public double? Value { get; set; }

        public ICollection<CoachRecordStat> Stats { get; set; } = [];

        public ICollection<CoachRecordExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CoachRecord>
        {
            public void Configure(EntityTypeBuilder<CoachRecord> builder)
            {
                builder.ToTable(nameof(CoachRecord));

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

                builder.Property(x => x.Value)
                    .HasPrecision(18, 6);

                builder.HasOne(x => x.Coach)
                    .WithMany(x => x.Records)
                    .HasForeignKey(x => x.CoachId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasMany(x => x.Stats)
                    .WithOne(x => x.CoachRecord)
                    .HasForeignKey(x => x.CoachRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }

}
