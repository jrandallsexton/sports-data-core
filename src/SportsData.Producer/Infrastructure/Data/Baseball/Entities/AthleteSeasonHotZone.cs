using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

public class AthleteSeasonHotZone : CanonicalEntityBase<Guid>
{
    public Guid AthleteSeasonId { get; set; }

    public int ConfigurationId { get; set; }

    public string? Name { get; set; }

    public bool Active { get; set; }

    public int SplitTypeId { get; set; }

    public int Season { get; set; }

    public int SeasonType { get; set; }

    public ICollection<AthleteSeasonHotZoneEntry> Entries { get; set; } = new List<AthleteSeasonHotZoneEntry>();

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeasonHotZone>
    {
        public void Configure(EntityTypeBuilder<AthleteSeasonHotZone> builder)
        {
            builder.ToTable(nameof(AthleteSeasonHotZone));
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.Name).HasMaxLength(50);

            builder.HasIndex(t => new { t.AthleteSeasonId, t.ConfigurationId, t.Season, t.SeasonType });

            builder.HasMany(t => t.Entries)
                .WithOne(e => e.HotZone)
                .HasForeignKey(e => e.AthleteSeasonHotZoneId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
