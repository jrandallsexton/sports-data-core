using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// An LLM provider fleet (DeepSeek, Anthropic, OpenAI, Google, and the
    /// long tail as it earns its way in). A provider is who MAKES the
    /// model; HOW a model is reached is <see cref="Model.Gateway"/> — an
    /// aggregator like OpenRouter is a gateway, not a provider, so a
    /// routed Claude row still belongs to Anthropic and its cutoff/cost
    /// metadata stays truthful. (The 2026-08-08 first-party-only decision,
    /// docs/metrics-modeling/matchup-preview-data-inputs.md §6, was
    /// amended 2026-09-03 to admit OpenRouter for the Model Consensus
    /// Lab's many-model audition; see docs/features/model-consensus-lab.md.)
    /// A provider row pairs with a code-side first-party client resolved
    /// via <see cref="Kind"/> — adding a provider means a new client anyway,
    /// so the enum being code-bound is honest.
    /// Credentials live in AppConfig, never here.
    /// </summary>
    public class ModelProvider : CanonicalEntityBase<Guid>
    {
        public required string Name { get; set; }

        /// <summary>Maps the row to its client implementation in the factory.</summary>
        public ModelProviderKind Kind { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public ICollection<Model> Models { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<ModelProvider>
        {
            public void Configure(EntityTypeBuilder<ModelProvider> builder)
            {
                builder.ToTable(nameof(ModelProvider));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
                builder.HasIndex(x => x.Name).IsUnique();

                builder.Property(x => x.Kind)
                    .HasConversion<int>()
                    .IsRequired();

                builder.Property(x => x.Description).HasMaxLength(256);
            }
        }
    }

    public enum ModelProviderKind
    {
        DeepSeek = 0,
        Anthropic = 1,
        OpenAi = 2,
        Google = 3,

        /// <summary>
        /// A provider with NO first-party client (the long tail: xAI,
        /// Alibaba, Moonshot, ...): its models are reachable only through
        /// a gateway row (Model.Gateway != None). Avoids enum churn per
        /// long-tail maker — if one ever earns a first-party client, it
        /// gets a real Kind value then.
        /// </summary>
        GatewayOnly = 99
    }
}
