using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// An LLM model identity record (design:
    /// docs/metrics-modeling/matchup-preview-data-inputs.md §6; seed data:
    /// docs/metrics-modeling/llm-training-dates.md). The knowledge cutoff
    /// is a DECLARED classification input from provider documentation —
    /// not proof a game was unseen — which is why the evidence source and
    /// verification date ride alongside it. The scoring harness labels
    /// each experiment lower-risk vs higher-risk by comparing the game's
    /// StartDateUtc against <see cref="KnowledgeCutoffUtc"/>.
    /// IsDefault marks the single model powering production generation —
    /// the pre-season "model selection" is literally this flag.
    /// </summary>
    public class Model : CanonicalEntityBase<Guid>
    {
        public Guid ModelProviderId { get; set; }

        public ModelProvider? ModelProvider { get; set; }

        /// <summary>Display name (unique), e.g. "Claude Haiku 4.5".</summary>
        public required string Name { get; set; }

        /// <summary>
        /// Exact API identifier sent on the wire FOR THIS ROUTE: the
        /// provider's native id when <see cref="Gateway"/> is None (e.g.
        /// "claude-haiku-4-5"), the gateway's namespaced id when routed
        /// (e.g. OpenRouter's "anthropic/claude-sonnet-4.5"). Exact ids
        /// only — provider aliases silently swap weights mid-season.
        /// </summary>
        public required string ApiModelId { get; set; }

        /// <summary>
        /// How this model is reached. None = first-party client (the
        /// provider's <see cref="ModelProvider.Kind"/> picks the
        /// implementation); OpenRouter = via the aggregator gateway
        /// (admitted 2026-09-03 for the Model Consensus Lab's audition
        /// breadth). The same weights reached two ways are DIFFERENT
        /// evaluands (routing/sampling can differ) — model them as two
        /// rows under the SAME provider; their ApiModelIds differ by
        /// construction (gateway ids are namespaced), so the
        /// (ModelProviderId, ApiModelId) unique index keeps them apart.
        /// </summary>
        public ModelGateway Gateway { get; set; } = ModelGateway.None;

        public DateTime? ReleaseDate { get; set; }

        /// <summary>
        /// Declared TRAINING-data cutoff (the later of any published
        /// "training" vs "reliable knowledge" dates — training exposure is
        /// what contamination risk cares about). Null = provider does not
        /// publish (e.g. DeepSeek) — the harness treats unknown as
        /// higher-risk.
        /// </summary>
        public DateTime? KnowledgeCutoffUtc { get; set; }

        /// <summary>Where the cutoff claim comes from (doc URL / note).</summary>
        public string? CutoffEvidence { get; set; }

        /// <summary>When the cutoff was last verified against provider docs.</summary>
        public DateTime? CutoffVerifiedUtc { get; set; }

        /// <summary>Cost metadata (USD per million tokens); informational.</summary>
        public decimal? InputCostPerMTok { get; set; }

        public decimal? OutputCostPerMTok { get; set; }

        public bool IsActive { get; set; } = true;

        /// <summary>The single model powering production generation.</summary>
        public bool IsDefault { get; set; }

        /// <summary>PostgreSQL xmin concurrency token — operator-edited via the management UI.</summary>
        public uint RowVersion { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Model>
        {
            public void Configure(EntityTypeBuilder<Model> builder)
            {
                builder.ToTable(nameof(Model));
                builder.HasKey(x => x.Id);

                builder.HasOne(x => x.ModelProvider)
                    .WithMany(p => p.Models)
                    .HasForeignKey(x => x.ModelProviderId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
                builder.HasIndex(x => x.Name).IsUnique();

                builder.Property(x => x.ApiModelId).HasMaxLength(100).IsRequired();
                builder.HasIndex(x => new { x.ModelProviderId, x.ApiModelId }).IsUnique();

                builder.Property(x => x.Gateway)
                    .HasConversion<int>()
                    .IsRequired();

                builder.Property(x => x.CutoffEvidence).HasMaxLength(512);

                builder.Property(x => x.InputCostPerMTok).HasPrecision(10, 4);
                builder.Property(x => x.OutputCostPerMTok).HasPrecision(10, 4);

                builder.Property(x => x.RowVersion)
                    .HasColumnName("xmin")
                    .HasColumnType("xid")
                    .IsRowVersion();

                // ONE production default across all models, enforced at the
                // database (same discipline as Prompt default slots).
                builder.HasIndex(x => x.IsDefault)
                    .IsUnique()
                    .HasFilter("\"IsDefault\"")
                    .HasDatabaseName("IX_Model_SingleDefault");
            }
        }
    }

    /// <summary>
    /// How a model is reached — a routing attribute, NOT a provider:
    /// OpenRouter is a gateway we communicate through; the provider (who
    /// made the model) stays on <see cref="Model.ModelProviderId"/>.
    /// </summary>
    public enum ModelGateway
    {
        /// <summary>Direct first-party client, resolved by the provider's Kind.</summary>
        None = 0,

        OpenRouter = 1
    }
}
