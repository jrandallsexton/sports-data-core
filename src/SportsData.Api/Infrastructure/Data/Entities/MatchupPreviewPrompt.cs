using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Api.Application.Previews;
using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// The exact data payload assembled for a matchup-preview LLM call —
    /// captured on every real generation (linked to the resulting
    /// <see cref="MatchupPreview"/>), on admin dry-run captures, and on
    /// experiment runs (which also store the model's raw response here and
    /// NEVER write a MatchupPreview — the picks page reads newest-non-
    /// rejected per contest, so an experimental preview row would shadow a
    /// prior season's real preview). The full prompt is
    /// PromptText + "\n\n" + PayloadJson + EditorNote, all stored here.
    /// Rows are kept forever — they are the backtest corpus
    /// (payload x model x prompt vs actual outcome).
    /// </summary>
    public class MatchupPreviewPrompt : CanonicalEntityBase<Guid>
    {
        public Guid ContestId { get; set; }

        public Sport Sport { get; set; }

        /// <summary>Set when a real generation persisted a preview; null for dry-run captures.</summary>
        public Guid? MatchupPreviewId { get; set; }

        public MatchupPreview? MatchupPreview { get; set; }

        public required string PromptVersion { get; set; }

        /// <summary>
        /// The Prompt entity that supplied the instructions; null for
        /// captures from the blob era. Deliberately no FK — prompts may be
        /// deleted by the management UI, and the capture's PromptText is the
        /// provenance record either way.
        /// </summary>
        public Guid? PromptId { get; set; }

        /// <summary>
        /// The instruction text EXACTLY as sent — stored per capture because
        /// a blob can be edited in place (ReloadPromptAsync), which would
        /// make version-based reconstruction lie about what the model saw.
        /// </summary>
        public required string PromptText { get; set; }

        /// <summary>The serialized matchup DTO appended to the prompt — the data part.</summary>
        public required string PayloadJson { get; set; }

        /// <summary>Rejection-feedback text appended to the prompt, when present.</summary>
        public string? EditorNote { get; set; }

        /// <summary>Character count of the full rendered prompt (instructions + payload + note).</summary>
        public int CharCount { get; set; }

        /// <summary>Rough token estimate (chars / 4) for budget visibility.</summary>
        public int EstTokens { get; set; }

        public PreviewGenerationMode Mode { get; set; }

        /// <summary>Model name, when the run called the model (Generate/Experiment).</summary>
        public string? Model { get; set; }

        /// <summary>
        /// The model's raw response (Generate/Experiment runs). Deliberately
        /// text, not jsonb — malformed responses are exactly the failures an
        /// experiment needs to record.
        /// </summary>
        public string? RawResponse { get; set; }

        /// <summary>Parse/validation problems recorded on experiment runs; null = clean.</summary>
        public string? ResponseValidationErrors { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<MatchupPreviewPrompt>
        {
            public void Configure(EntityTypeBuilder<MatchupPreviewPrompt> builder)
            {
                builder.ToTable(nameof(MatchupPreviewPrompt));
                builder.HasKey(x => x.Id);

                builder.HasIndex(x => x.ContestId);

                builder.HasOne(x => x.MatchupPreview)
                    .WithMany()
                    .HasForeignKey(x => x.MatchupPreviewId)
                    .OnDelete(DeleteBehavior.SetNull);

                builder.Property(x => x.Sport)
                    .HasConversion<int>()
                    .IsRequired();

                builder.Property(x => x.PromptVersion).HasMaxLength(50);

                builder.Property(x => x.PayloadJson)
                    .HasColumnType("jsonb")
                    .IsRequired();

                builder.Property(x => x.EditorNote).HasMaxLength(512);

                builder.Property(x => x.Mode)
                    .HasConversion<int>()
                    .IsRequired();

                builder.Property(x => x.Model).HasMaxLength(50);

                builder.Property(x => x.ResponseValidationErrors).HasMaxLength(1024);
            }
        }
    }
}
