using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities;

public class FootballCompetitionPlay : CompetitionPlay
{
    public CompetitionDrive? Drive { get; set; }

    public Guid? DriveId { get; set; }

    public double ClockValue { get; set; }

    public string? ClockDisplayValue { get; set; }

    public Guid? EndFranchiseSeasonId { get; set; }

    public int? StartDown { get; set; }

    public int? StartDistance { get; set; }

    public int? StartYardLine { get; set; }

    public int? StartYardsToEndzone { get; set; }

    public int? EndDown { get; set; }

    public int? EndDistance { get; set; }

    public int? EndYardLine { get; set; }

    public int? EndYardsToEndzone { get; set; }

    public int StatYardage { get; set; }

    public new class EntityConfiguration : IEntityTypeConfiguration<FootballCompetitionPlay>
    {
        public void Configure(EntityTypeBuilder<FootballCompetitionPlay> builder)
        {
            builder.Property(x => x.ClockDisplayValue).HasMaxLength(32);
            builder.Property(x => x.DriveId).IsRequired(false);

            builder.HasOne(x => x.Drive)
                .WithMany(x => x.Plays)
                .HasForeignKey(x => x.DriveId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
