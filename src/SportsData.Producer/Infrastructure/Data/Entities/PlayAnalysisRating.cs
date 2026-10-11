using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

/// <summary>
/// An admin's 0-4 star rating of one instant-analysis fact in the replay lab
/// (as SmackBot's preview ratings are). One rating per fact; the fact row
/// already belongs to a run, so ratings are per fact and run. Feeds the
/// "learning what's weak" analysis.
/// </summary>
public class PlayAnalysisRating : CanonicalEntityBase<Guid>
{
    public Guid PlayAnalysisId { get; set; }
    public PlayAnalysis PlayAnalysis { get; set; } = null!;

    public int Stars { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<PlayAnalysisRating>
    {
        public void Configure(EntityTypeBuilder<PlayAnalysisRating> builder)
        {
            builder.ToTable(nameof(PlayAnalysisRating), t =>
                t.HasCheckConstraint("CK_PlayAnalysisRating_Stars_Range", "\"Stars\" BETWEEN 0 AND 4"));

            builder.HasKey(x => x.Id);

            builder.HasOne(x => x.PlayAnalysis)
                .WithMany()
                .HasForeignKey(x => x.PlayAnalysisId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.PlayAnalysisId).IsUnique();
        }
    }
}
