using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Common
{
    public class Athlete : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public string? LastName { get; set; }

        public string? FirstName { get; set; }

        public string? DisplayName { get; set; }

        public string? ShortName { get; set; }

        public string? Slug { get; set; }

        public decimal WeightLb { get; set; } = -1;

        public string? WeightDisplay { get; set; }

        public decimal HeightIn { get; set; } = -1;

        public string? HeightDisplay { get; set; }

        public int Age { get; set; } = -1;

        public DateTime? DoB { get; set; }

        public Guid? BirthLocationId { get; set; }

        public Location? BirthLocation { get; set; }

        public string? ExperienceAbbreviation { get; set; }

        public string? ExperienceDisplayValue { get; set; }

        public int ExperienceYears { get; set; } = -1;

        public bool IsActive { get; set; }

        public Guid? StatusId { get; set; }

        public AthleteStatus? Status { get; set; }

        public ICollection<AthleteSeason> Seasons { get; set; } = new List<AthleteSeason>();

        public ICollection<AthleteImage> Images { get; set; } = new List<AthleteImage>();

        public ICollection<AthleteExternalId> ExternalIds { get; set; } = new List<AthleteExternalId>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<Athlete>
        {
            public void Configure(EntityTypeBuilder<Athlete> builder)
            {
                builder.ToTable(nameof(Athlete));
                builder.HasKey(t => t.Id);

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

                builder.Property(x => x.WeightLb)
                    .HasPrecision(5, 2);

                builder.Property(x => x.HeightIn)
                    .HasPrecision(5, 2);

                builder.Property(x => x.HeightDisplay)
                    .HasMaxLength(20);

                builder.Property(x => x.WeightDisplay)
                    .HasMaxLength(20);

                builder.Property(x => x.ExperienceAbbreviation)
                    .HasMaxLength(10);

                builder.Property(x => x.ExperienceDisplayValue)
                    .HasMaxLength(20);

                builder.Property(x => x.Slug)
                    .HasMaxLength(64);

                builder.HasOne(a => a.BirthLocation)
                    .WithMany()
                    .HasForeignKey(a => a.BirthLocationId);

                builder.HasOne(a => a.Status)
                    .WithMany()
                    .HasForeignKey(a => a.StatusId);

                builder.HasMany(x => x.Seasons)
                    .WithOne(x => x.Athlete)
                    .HasForeignKey(x => x.AthleteId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Images)
                    .WithOne(x => x.Athlete)
                    .HasForeignKey(x => x.AthleteId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.ExternalIds)
                    .WithOne(x => x.Athlete)
                    .HasForeignKey(x => x.AthleteId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }

    }
}
