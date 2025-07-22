using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonRecord : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonId { get; set; } // FK to FranchiseSeason

        public FranchiseSeason Season { get; set; } = null!;

        public Guid FranchiseId { get; set; } // denormalized for convenience

        public int SeasonYear { get; set; } // denormalized for convenience

        public required string Name { get; set; }

        public required string Abbreviation { get; set; }

        public required string DisplayName { get; set; }

        public required string ShortDisplayName { get; set; }

        public required string Description { get; set; }

        public required string Type { get; set; }

        public required string Summary { get; set; }

        public required string DisplayValue { get; set; }

        public double Value { get; set; }

        public List<FranchiseSeasonRecordStat> Stats { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRecord>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonRecord> builder)
            {
                builder.ToTable(nameof(FranchiseSeasonRecord));
                builder.HasKey(t => t.Id);

                builder.Property(t => t.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.Abbreviation)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(t => t.DisplayName)
                    .IsRequired()
                    .HasMaxLength(150);

                builder.Property(t => t.ShortDisplayName)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.Description)
                    .IsRequired()
                    .HasMaxLength(300);

                builder.Property(t => t.Type)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.Summary)
                    .IsRequired()
                    .HasMaxLength(300);

                builder.Property(t => t.DisplayValue)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.Value)
                    .IsRequired();

                builder.Property(t => t.SeasonYear)
                    .IsRequired();

                builder.Property(t => t.FranchiseId)
                    .IsRequired();

                builder.HasOne(t => t.Season)
                    .WithMany(s => s.Records)
                    .HasForeignKey(t => t.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }

    }
}