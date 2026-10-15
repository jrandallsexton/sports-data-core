using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// The lineage a Model Consensus Lab seat follows across versions
    /// ("Qwen" holds Qwen 3 Max, then Qwen 3.8 Max, ...). Coarser than a
    /// <see cref="Model"/> (one row per exact weights + route) and finer
    /// than a <see cref="ModelProvider"/> (Anthropic holds two seats:
    /// Sonnet and Haiku). The lab matrix groups history by family, so
    /// retiring a version never drops its votes from past weeks; results
    /// stay attributed to the exact Model that produced them. At most one
    /// lab-reachable version per family is active at a time
    /// (IX_Model_SingleActiveLabSeat). A family is "on" when one of its
    /// versions is active, so it carries no IsActive of its own.
    /// Design: docs/features/model-family.md.
    /// </summary>
    public class ModelFamily : CanonicalEntityBase<Guid>
    {
        /// <summary>Display name (unique), e.g. "Qwen", "Claude Sonnet".</summary>
        public required string Name { get; set; }

        public string? Description { get; set; }

        public ICollection<Model> Models { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<ModelFamily>
        {
            public void Configure(EntityTypeBuilder<ModelFamily> builder)
            {
                builder.ToTable(nameof(ModelFamily));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
                builder.HasIndex(x => x.Name).IsUnique();

                builder.Property(x => x.Description).HasMaxLength(512);
            }
        }
    }
}
