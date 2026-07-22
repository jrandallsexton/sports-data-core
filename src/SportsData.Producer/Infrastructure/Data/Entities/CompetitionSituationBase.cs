using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    /// <summary>
    /// Shared base for per-competition situation snapshots. Mirrors the
    /// CompetitionPlayBase / FootballCompetitionPlay / BaseballCompetitionPlay
    /// TPH split: one sport-agnostic table (`CompetitionSituation`) backs the
    /// sport-specific subclasses, with EF auto-generating the discriminator.
    ///
    /// The sport-specific fields (football Down/Distance/YardLine; baseball
    /// balls/strikes/outs + baserunners) live on the derived types so a sport's
    /// situation shape can diverge cleanly instead of piling nullables on a
    /// shared parent. Only the shared keys + the Competition/LastPlay
    /// relationships live here.
    /// </summary>
    public abstract class CompetitionSituationBase : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionId { get; set; }
        public CompetitionBase Competition { get; set; } = null!;

        public Guid? LastPlayId { get; set; }

        public CompetitionPlayBase? LastPlay { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionSituationBase>
        {
            public void Configure(EntityTypeBuilder<CompetitionSituationBase> builder)
            {
                builder.ToTable("CompetitionSituation");

                builder.HasKey(x => x.Id);

                // -------- Parent: CompetitionBase (required) --------
                builder.HasOne(x => x.Competition)
                       .WithMany(c => c.Situations)
                       .HasForeignKey(x => x.CompetitionId)
                       .OnDelete(DeleteBehavior.Restrict);

                builder.HasIndex(x => x.CompetitionId);

                // -------- Optional: LastPlay --------
                builder.HasOne(x => x.LastPlay)
                    .WithMany(p => p.SituationsAsLastPlay)
                    .HasForeignKey(x => x.LastPlayId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasIndex(x => x.LastPlayId);
            }
        }
    }
}
