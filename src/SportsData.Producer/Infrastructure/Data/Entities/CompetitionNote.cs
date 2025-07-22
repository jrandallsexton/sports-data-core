using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionNote : CanonicalEntityBase<Guid>
    {
        public required string Type { get; set; }

        public required string Headline { get; set; }

        public Guid CompetitionId { get; set; } // FK to ContestCompetition

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionNote>
        {
            public void Configure(EntityTypeBuilder<CompetitionNote> builder)
            {
                builder.ToTable(nameof(CompetitionNote));
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Type).IsRequired().HasMaxLength(50);
                builder.Property(x => x.Headline).IsRequired().HasMaxLength(250);
                builder.Property(x => x.CompetitionId).IsRequired();
            }
        }
    }
}
