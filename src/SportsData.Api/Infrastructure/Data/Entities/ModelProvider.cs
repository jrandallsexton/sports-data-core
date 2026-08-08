using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// An LLM provider fleet (DeepSeek, Anthropic, OpenAI, Google —
    /// first-party clients only, per the 2026-08-08 decision; OpenRouter
    /// and Ollama Cloud evaluated and passed, see
    /// docs/metrics-modeling/matchup-preview-data-inputs.md §6).
    /// A provider row pairs with a code-side IProvideAiCommunication
    /// implementation resolved via <see cref="Kind"/> — adding a provider
    /// means a new client anyway, so the enum being code-bound is honest.
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
        Google = 3
    }
}
