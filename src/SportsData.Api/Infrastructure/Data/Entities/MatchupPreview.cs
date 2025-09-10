using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class MatchupPreview : CanonicalEntityBase<Guid>
    {
        public Guid ContestId { get; set; } // from Producer

        public string? Overview { get; set; }

        public string? Analysis { get; set; }

        public string? Prediction { get; set; }

        public Guid? PredictedStraightUpWinner { get; set; }

        public Guid? PredictedSpreadWinner { get; set; }

        public OverUnderPrediction OverUnderPrediction { get; set; } = OverUnderPrediction.None;

        public int? AwayScore { get; set; }

        public int? HomeScore { get; set; }

        public int? IterationsRequired { get; set; }

        public string? Model { get; set; }

        public string? ValidationErrors { get; set; }

        public string? PromptVersion { get; set; }

        public DateTime? ApprovedUtc { get; set; }

        public DateTime? RejectedUtc { get; set; }

        public string? RejectionNote { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<MatchupPreview>
        {
            public void Configure(EntityTypeBuilder<MatchupPreview> builder)
            {
                builder.ToTable(nameof(MatchupPreview));
                builder.HasKey(x => x.Id);
                //builder.HasIndex(x => new { x.ContestId, x.Model, x.PromptVersion }).IsUnique();

                builder.Property(x => x.Overview).HasMaxLength(512);
                builder.Property(x => x.Analysis).HasMaxLength(1024);
                builder.Property(x => x.Prediction).HasMaxLength(768);

                builder.Property(x => x.Model).HasMaxLength(50);
                builder.Property(x => x.PromptVersion).HasMaxLength(50);

                builder.Property(x => x.ValidationErrors).HasMaxLength(1024);
                builder.Property(x => x.RejectionNote).HasMaxLength(512);

                builder.Property(l => l.OverUnderPrediction)
                    .HasConversion<int>()
                    .IsRequired();
            }
        }
    }

    public enum OverUnderPrediction
    {
        None = 0,
        Over = 1,
        Under = 2
    }
}
