using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public class Athlete : CanonicalEntityBase<Guid>
    {
        public required string LastName { get; set; }

        public required string FirstName { get; set; }

        public required string DisplayName { get; set; }

        public required string ShortName { get; set; }

        public decimal WeightLb { get; set; } = -1;

        public string? WeightDisplay { get; set; }

        public decimal HeightIn { get; set; } = -1;

        public required string HeightDisplay { get; set; }

        public int Age { get; set; } = -1;

        public DateTime DoB { get; set; }

        // TODO: Birth location info

        public int CurrentExperience { get; set; } = -1;

        public bool IsActive { get; set; }

        public List<AthleteSeason> Seasons { get; set; } = [];

        public List<AthleteImage> Images { get; set; } = [];

        public List<AthleteExternalId> ExternalIds { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Athlete>
        {
            public void Configure(EntityTypeBuilder<Athlete> builder)
            {
                builder.ToTable("Athlete");
                builder.HasKey(t => t.Id);
                //builder.Property(x => x.Id).ValueGeneratedNever();

                builder.Property(x => x.FirstName)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(x => x.LastName)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(x => x.DisplayName)
                    .IsRequired()
                    .HasMaxLength(150);

                builder.Property(x => x.ShortName)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(x => x.HeightDisplay)
                    .IsRequired()
                    .HasMaxLength(20);

                builder.Property(x => x.WeightDisplay)
                    .HasMaxLength(20);
            }
        }

    }
}
