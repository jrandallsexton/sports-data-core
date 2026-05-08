using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities
{
    // Per-competitor probable role for an MLB game. Today only the
    // probable starting pitcher (Name = "probableStartingPitcher") shows
    // up in ESPN's payload, but the array shape leaves room for future
    // roles (closer, etc.). Modeled as a 1:N collection on
    // BaseballCompetitionCompetitor.
    //
    // AthleteSeasonId is required: per the established not-sourced
    // pattern, the processor throws ExternalDocumentNotSourcedException
    // when the athlete ref can't be resolved, so this row is never
    // persisted with a missing FK. An empty Probable is worthless on
    // the matchup card.
    //
    // See docs/competition-competitor-split.md (Phase 2 outline) and
    // docs/competition-competitor-probables.md.
    public class CompetitionCompetitorProbable : CanonicalEntityBase<Guid>
    {
        public required Guid CompetitionCompetitorId { get; set; }

        public BaseballCompetitionCompetitor CompetitionCompetitor { get; set; } = null!;

        public required Guid AthleteSeasonId { get; set; }

        public AthleteSeason AthleteSeason { get; set; } = null!;

        public required int EspnPlayerId { get; set; }

        public string? Name { get; set; }

        public string? DisplayName { get; set; }

        public string? ShortDisplayName { get; set; }

        public string? Abbreviation { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionCompetitorProbable>
        {
            public void Configure(EntityTypeBuilder<CompetitionCompetitorProbable> builder)
            {
                builder.ToTable(nameof(CompetitionCompetitorProbable));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.CompetitionCompetitorId).IsRequired();
                builder.Property(x => x.AthleteSeasonId).IsRequired();
                builder.Property(x => x.EspnPlayerId).IsRequired();

                builder.Property(x => x.Name).HasMaxLength(50);
                builder.Property(x => x.DisplayName).HasMaxLength(100);
                builder.Property(x => x.ShortDisplayName).HasMaxLength(50);
                builder.Property(x => x.Abbreviation).HasMaxLength(10);

                // FK: BaseballCompetitionCompetitor (parent) -> Probables
                builder.HasOne(p => p.CompetitionCompetitor)
                    .WithMany(cc => cc.Probables)
                    .HasForeignKey(p => p.CompetitionCompetitorId)
                    .OnDelete(DeleteBehavior.Cascade);

                // FK: AthleteSeason (reference). Restrict so deleting an
                // AthleteSeason doesn't cascade-cull historical probables.
                builder.HasOne(p => p.AthleteSeason)
                    .WithMany()
                    .HasForeignKey(p => p.AthleteSeasonId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Uniqueness: one probable role per competitor at a time.
                // Today only "probableStartingPitcher" exists, but the
                // (CompetitorId, Name) pair is the natural key if more
                // roles arrive.
                builder.HasIndex(x => new { x.CompetitionCompetitorId, x.Name })
                    .IsUnique();
            }
        }
    }
}
