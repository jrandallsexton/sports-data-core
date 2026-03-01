using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthleteSeason : CanonicalEntityBase<Guid>, IHasExternalIds
{
    public Guid AthleteId { get; set; }
    public Athlete Athlete { get; set; } = default!;

    public Guid? FranchiseSeasonId { get; set; }

    public Guid PositionId { get; set; }
    public AthletePosition Position { get; set; } = default!;

    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? DisplayName { get; set; }
    public string? ShortName { get; set; }

    public string? Slug { get; set; }

    public decimal WeightLb { get; set; }
    public string? WeightDisplay { get; set; }

    public decimal HeightIn { get; set; }
    public string? HeightDisplay { get; set; }

    public string? Jersey { get; set; }

    public string? ExperienceAbbreviation { get; set; }
    public string? ExperienceDisplayValue { get; set; }
    public int ExperienceYears { get; set; }

    public bool IsActive { get; set; }

    public Guid? StatusId { get; set; }
    public AthleteStatus? Status { get; set; }

    public ICollection<AthleteSeasonStatistic> Statistics { get; set; } = [];

    public ICollection<AthleteSeasonExternalId> ExternalIds { get; set; } = [];

    public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeason>
    {
        public void Configure(EntityTypeBuilder<AthleteSeason> builder)
        {
            builder.ToTable(nameof(AthleteSeason));
            builder.HasKey(t => t.Id);

            builder.Property(x => x.FirstName).HasMaxLength(100);
            builder.Property(x => x.LastName).HasMaxLength(100);
            builder.Property(x => x.DisplayName).HasMaxLength(150);
            builder.Property(x => x.ShortName).HasMaxLength(100);

            builder.Property(x => x.Slug).HasMaxLength(64);

            builder.Property(x => x.WeightLb).HasPrecision(5, 2);
            builder.Property(x => x.WeightDisplay).HasMaxLength(20);

            builder.Property(x => x.HeightIn).HasPrecision(5, 2);
            builder.Property(x => x.HeightDisplay).HasMaxLength(20);

            builder.Property(x => x.Jersey).HasMaxLength(10);

            builder.Property(x => x.ExperienceAbbreviation).HasMaxLength(10);
            builder.Property(x => x.ExperienceDisplayValue).HasMaxLength(20);

            builder.HasOne(x => x.Athlete)
                .WithMany(x => x.Seasons)
                .HasForeignKey(x => x.AthleteId);

            builder.HasOne(x => x.Position)
                .WithMany()
                .HasForeignKey(x => x.PositionId);

            builder.HasOne(x => x.Status)
                .WithMany()
                .HasForeignKey(x => x.StatusId);

            builder.HasMany(x => x.Statistics)
                .WithOne(x => x.AthleteSeason)
                .HasForeignKey(x => x.AthleteSeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(x => x.ExternalIds)
                .WithOne(x => x.AthleteSeason)
                .HasForeignKey(x => x.AthleteSeasonId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
