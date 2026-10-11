using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

/// <summary>
/// One instant-analysis fact generated for a live play
/// ("his 2nd interception of the season"). A play's facts form one set per
/// run: <see cref="AnalysisRunId"/> is null for live analysis and set for
/// lab replays, so replays never mix with what users saw.
///
/// Linked to the triggering play by a required FK so a game can be audited
/// play by play after the final. See docs/features/instant-analysis/.
/// </summary>
public class PlayAnalysis : CanonicalEntityBase<Guid>
{
    public Guid ContestId { get; set; }

    public Guid CompetitionId { get; set; }

    public Guid PlayId { get; set; }
    public CompetitionPlayBase Play { get; set; } = null!;

    /// <summary>Null for live analysis; set for a lab replay run.</summary>
    public Guid? AnalysisRunId { get; set; }

    /// <summary>Position of this fact within its play's set (0-based).</summary>
    public int Ordinal { get; set; }

    public string FactType { get; set; } = string.Empty;

    /// <summary>
    /// The fact's numbers plus provenance (sources used, sequence counted,
    /// "as of" cut-off), as JSON.
    /// </summary>
    public string Facts { get; set; } = "{}";

    public string Text { get; set; } = string.Empty;

    public int Priority { get; set; }

    public int TemplateVersion { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<PlayAnalysis>
    {
        public void Configure(EntityTypeBuilder<PlayAnalysis> builder)
        {
            builder.ToTable(nameof(PlayAnalysis));

            builder.HasKey(x => x.Id);

            builder.Property(x => x.FactType)
                .HasMaxLength(64)
                .IsRequired();

            builder.Property(x => x.Facts)
                .HasColumnType("jsonb")
                .IsRequired();

            builder.Property(x => x.Text)
                .HasMaxLength(512)
                .IsRequired();

            builder.HasOne(x => x.Play)
                .WithMany()
                .HasForeignKey(x => x.PlayId)
                .OnDelete(DeleteBehavior.Cascade);

            // Post-game audit: walk a competition's plays with their facts.
            builder.HasIndex(x => new { x.CompetitionId, x.PlayId });

            // One set per play per run: a redelivered or retried job collides
            // on Ordinal 0 instead of writing a second set. NULLS NOT DISTINCT
            // makes this hold for live rows (AnalysisRunId null) too.
            builder.HasIndex(x => new { x.PlayId, x.AnalysisRunId, x.Ordinal })
                .IsUnique()
                .AreNullsDistinct(false);
        }
    }
}
