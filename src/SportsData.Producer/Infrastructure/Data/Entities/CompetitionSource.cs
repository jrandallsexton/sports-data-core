using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionSource : CanonicalEntityBase<int>
    {
        public required string Description { get; set; }

        public required string State { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionSource>
        {
            public void Configure(EntityTypeBuilder<CompetitionSource> builder)
            {
                builder.Property(c => c.Id)
                    .ValueGeneratedNever();

                builder.Property(c => c.Description)
                    .IsRequired()
                    .HasMaxLength(75);

                builder.Property(c => c.State)
                    .IsRequired()
                    .HasMaxLength(75);

                builder.ToTable("lkCompetitionSource");

                builder.HasData(
                    new CompetitionSource
                    {
                        Id = 1,
                        Description = "basic/manual",
                        State = "basic",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new CompetitionSource
                    {
                        Id = 2,
                        Description = "feed",
                        State = "full",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new CompetitionSource
                    {
                        Id = 4,
                        Description = "official",
                        State = "full",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                );
            }
        }
    }
}
