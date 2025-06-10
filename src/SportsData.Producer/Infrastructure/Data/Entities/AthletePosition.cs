using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class AthletePosition : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public required string Name { get; set; }

        public required string DisplayName { get; set; }

        public required string Abbreviation { get; set; }

        public bool Leaf { get; set; }

        /// <summary>
        /// Nullable FK to support position hierarchy (e.g., "Defense" → "Linebacker")
        /// </summary>
        public Guid? ParentId { get; set; }

        public AthletePosition? Parent { get; set; }

        public ICollection<AthletePosition> Children { get; set; } = new List<AthletePosition>();

        public List<AthletePositionExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<AthletePosition>
        {
            public void Configure(EntityTypeBuilder<AthletePosition> builder)
            {
                builder.ToTable("AthletePosition");

                builder.HasKey(p => p.Id);

                builder.Property(p => p.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(p => p.DisplayName)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(p => p.Abbreviation)
                    .IsRequired()
                    .HasMaxLength(20);

                builder.Property(p => p.Leaf)
                    .IsRequired();

                builder.HasOne(p => p.Parent)
                    .WithMany(p => p.Children)
                    .HasForeignKey(p => p.ParentId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }

    }
}