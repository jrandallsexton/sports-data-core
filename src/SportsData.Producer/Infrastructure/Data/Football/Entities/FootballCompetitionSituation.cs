using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities;

/// <summary>
/// Football situation snapshot (down / distance / yard line / timeouts).
/// Backed by the shared `CompetitionSituation` TPH table; the football fields +
/// their check constraints live here so the baseball subtype isn't dragged into
/// football-shaped validation.
/// </summary>
public class FootballCompetitionSituation : CompetitionSituationBase
{
    public int Down { get; set; }

    public int Distance { get; set; }

    public int YardLine { get; set; }

    public bool IsRedZone { get; set; }

    public int AwayTimeouts { get; set; }

    public int HomeTimeouts { get; set; }

    public new class EntityConfiguration : IEntityTypeConfiguration<FootballCompetitionSituation>
    {
        public void Configure(EntityTypeBuilder<FootballCompetitionSituation> builder)
        {
            builder.Property(x => x.Down).IsRequired();
            builder.Property(x => x.Distance).IsRequired();
            builder.Property(x => x.YardLine).IsRequired();
            builder.Property(x => x.IsRedZone).IsRequired();
            builder.Property(x => x.AwayTimeouts).IsRequired();
            builder.Property(x => x.HomeTimeouts).IsRequired();

            // -------- Check constraints (PostgreSQL-friendly) --------
            builder.ToTable(t =>
            {
                // -1|0..4 downs (-1|0 = no down, 1-4 = valid downs) -1|0 at end of game
                t.HasCheckConstraint("CK_CompetitionSituation_Down", "\"Down\" BETWEEN -1 AND 4");
                // 0..100 yard line (covers goal line/touchback edges)
                t.HasCheckConstraint("CK_CompetitionSituation_YardLine", "\"YardLine\" BETWEEN 0 AND 100");
                // Distance floor of -110 (NOT >= 0): ESPN ships negative distances
                // and the mapper does not clamp Distance (unlike timeouts), so the
                // constraint must admit them or valid plays would fail to insert.
                t.HasCheckConstraint("CK_CompetitionSituation_Distance", "\"Distance\" >= -110");
                // Timeouts >= 0 (tighten to 0..3 if you want to enforce NCAA max)
                t.HasCheckConstraint("CK_CompetitionSituation_AwayTimeouts", "\"AwayTimeouts\" >= 0");
                t.HasCheckConstraint("CK_CompetitionSituation_HomeTimeouts", "\"HomeTimeouts\" >= 0");
            });
        }
    }
}
