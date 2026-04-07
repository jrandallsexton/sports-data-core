using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

public class AthleteSeasonHotZoneEntry : CanonicalEntityBase<Guid>
{
    public Guid AthleteSeasonHotZoneId { get; set; }

    public AthleteSeasonHotZone HotZone { get; set; } = default!;

    public int ZoneId { get; set; }

    public double XMin { get; set; }

    public double XMax { get; set; }

    public double YMin { get; set; }

    public double YMax { get; set; }

    public int? AtBats { get; set; }

    public int? Hits { get; set; }

    public double? BattingAvg { get; set; }

    public double? BattingAvgScore { get; set; }

    public double? Slugging { get; set; }

    public double? SluggingScore { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeasonHotZoneEntry>
    {
        public void Configure(EntityTypeBuilder<AthleteSeasonHotZoneEntry> builder)
        {
            builder.ToTable(nameof(AthleteSeasonHotZoneEntry));
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.BattingAvg).HasPrecision(7, 4);
            builder.Property(t => t.BattingAvgScore).HasPrecision(7, 4);
            builder.Property(t => t.Slugging).HasPrecision(7, 4);
            builder.Property(t => t.SluggingScore).HasPrecision(7, 4);
            builder.Property(t => t.XMin).HasPrecision(7, 2);
            builder.Property(t => t.XMax).HasPrecision(7, 2);
            builder.Property(t => t.YMin).HasPrecision(7, 2);
            builder.Property(t => t.YMax).HasPrecision(7, 2);
        }
    }
}
