using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

/// <summary>
/// One instant-analysis lab replay of a completed game. <see cref="CanonicalEntityBase{T}.Id"/>
/// is the run's AnalysisRunId, stamped on every <see cref="PlayAnalysis"/> row
/// the run writes. Dropped facts are kept here (live drops go to the logs only)
/// so the lab can show why a fact wasn't said. See docs/features/instant-analysis/.
/// </summary>
public class PlayAnalysisRun : CanonicalEntityBase<Guid>
{
    public Guid ContestId { get; set; }

    public Guid CompetitionId { get; set; }

    public int PlayCount { get; set; }

    public int FactCount { get; set; }

    public int DroppedCount { get; set; }

    public int TemplateVersion { get; set; }

    /// <summary>
    /// JSON array of the facts considered and not produced:
    /// <c>{ playId, factType, kind, athleteSeasonId, reason }</c>.
    /// </summary>
    public string DroppedFacts { get; set; } = "[]";

    public DateTime? CompletedUtc { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<PlayAnalysisRun>
    {
        public void Configure(EntityTypeBuilder<PlayAnalysisRun> builder)
        {
            builder.ToTable(nameof(PlayAnalysisRun));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.DroppedFacts)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.HasIndex(x => x.ContestId);
        }
    }
}
