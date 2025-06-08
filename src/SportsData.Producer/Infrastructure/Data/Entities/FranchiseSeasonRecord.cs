using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

using System.ComponentModel.DataAnnotations.Schema;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonRecord : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonId { get; set; } // FK to FranchiseSeason

        public FranchiseSeason Season { get; set; }

        public Guid FranchiseId { get; set; } // denormalized for convenience

        public int SeasonYear { get; set; } // denormalized for convenience

        public string RecordId { get; set; } // ESPN's string-based id

        public string Name { get; set; }

        public string Abbreviation { get; set; }

        public string DisplayName { get; set; }

        public string ShortDisplayName { get; set; }

        public string Description { get; set; }

        public string Type { get; set; }

        public string Summary { get; set; }

        public string DisplayValue { get; set; }

        public double Value { get; set; }

        public List<FranchiseSeasonRecordStat> Stats { get; set; } = new();

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRecord>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonRecord> builder)
            {
                builder.ToTable("FranchiseSeasonRecord");
                builder.HasKey(t => t.Id);

                builder.Property(t => t.Type)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.DisplayValue)
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

    public class FranchiseSeasonRecordStat
    {
        public Guid Id { get; set; }

        public Guid FranchiseSeasonRecordId { get; set; }

        public string Name { get; set; }

        public string DisplayName { get; set; }

        public string ShortDisplayName { get; set; }

        public string Description { get; set; }

        public string Abbreviation { get; set; }

        public string Type { get; set; }

        public double Value { get; set; }

        public string DisplayValue { get; set; }

        [ForeignKey(nameof(FranchiseSeasonRecordId))]
        public FranchiseSeasonRecord Record { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRecordStat>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonRecordStat> builder)
            {
                builder.ToTable("FranchiseSeasonRecordStat");
                builder.HasKey(t => t.Id);

                builder.Property(t => t.Name).HasMaxLength(100);
                builder.Property(t => t.DisplayName).HasMaxLength(100);
                builder.Property(t => t.ShortDisplayName).HasMaxLength(50);
                builder.Property(t => t.Description).HasMaxLength(200);
                builder.Property(t => t.Abbreviation).HasMaxLength(20);
                builder.Property(t => t.Type).HasMaxLength(50);
                builder.Property(t => t.DisplayValue).HasMaxLength(100);
                builder.Property(t => t.Value).IsRequired();

                builder.HasOne(t => t.Record)
                    .WithMany(r => r.Stats)
                    .HasForeignKey(t => t.FranchiseSeasonRecordId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }

    }
}