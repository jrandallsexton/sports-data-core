using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

/// <summary>
/// Baseball situation snapshot: count (balls/strikes/outs) + baserunner
/// occupancy. Backed by the shared `CompetitionSituation` TPH table. The
/// baserunner refs are season-scoped athlete URLs, so they resolve to
/// AthleteSeason (nullable — a base can be empty). Replaces the previous
/// behavior where baseball situations were written into the football-shaped
/// entity with Down/Distance/YardLine = 0 and the runners discarded.
/// </summary>
public class BaseballCompetitionSituation : CompetitionSituationBase
{
    public int Balls { get; set; }

    public int Strikes { get; set; }

    public int Outs { get; set; }

    public Guid? OnFirstAthleteSeasonId { get; set; }

    public AthleteSeason? OnFirstAthleteSeason { get; set; }

    public Guid? OnSecondAthleteSeasonId { get; set; }

    public AthleteSeason? OnSecondAthleteSeason { get; set; }

    public Guid? OnThirdAthleteSeasonId { get; set; }

    public AthleteSeason? OnThirdAthleteSeason { get; set; }

    public ICollection<BaseballCompetitionSituationNote> Notes { get; set; }
        = new List<BaseballCompetitionSituationNote>();

    public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionSituation>
    {
        public void Configure(EntityTypeBuilder<BaseballCompetitionSituation> builder)
        {
            builder.Property(x => x.Balls).IsRequired();
            builder.Property(x => x.Strikes).IsRequired();
            builder.Property(x => x.Outs).IsRequired();

            // Baserunner occupancy → AthleteSeason. Restrict so a situation
            // snapshot never cascades athlete deletes.
            builder.HasOne(x => x.OnFirstAthleteSeason)
                .WithMany()
                .HasForeignKey(x => x.OnFirstAthleteSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.OnSecondAthleteSeason)
                .WithMany()
                .HasForeignKey(x => x.OnSecondAthleteSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(x => x.OnThirdAthleteSeason)
                .WithMany()
                .HasForeignKey(x => x.OnThirdAthleteSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(x => x.OnFirstAthleteSeasonId);
            builder.HasIndex(x => x.OnSecondAthleteSeasonId);
            builder.HasIndex(x => x.OnThirdAthleteSeasonId);

            builder.HasMany(x => x.Notes)
                .WithOne(n => n.Situation)
                .HasForeignKey(n => n.SituationId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
