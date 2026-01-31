using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Coach : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public required string LastName { get; set; }

        public required string FirstName { get; set; }

        public string? Title { get; set; }

        public string? Nickname { get; set; }

        public DateOnly? DateOfBirth { get; set; }

        public int Experience { get; set; }

        public ICollection<CoachRecord> Records { get; set; } = [];

        public ICollection<CoachSeason> Seasons { get; set; } = [];

        public ICollection<CoachExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<Coach>
        {
            public void Configure(EntityTypeBuilder<Coach> builder)
            {
                builder.ToTable(nameof(Coach));
                builder.HasKey(t => t.Id);

                builder.Property(t => t.Experience);
                builder.Property(t => t.FirstName).IsRequired().HasMaxLength(100);
                builder.Property(t => t.LastName).IsRequired().HasMaxLength(100);
                builder.Property(t => t.Nickname).HasMaxLength(100);
                builder.Property(t => t.Title).HasMaxLength(100);

                builder
                    .HasMany(t => t.Records)
                    .WithOne(r => r.Coach)
                    .HasForeignKey(r => r.CoachId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder
                    .HasMany(t => t.Seasons)
                    .WithOne(s => s.Coach)
                    .HasForeignKey(s => s.CoachId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder
                    .HasMany(t => t.ExternalIds)
                    .WithOne()
                    .HasForeignKey(e => e.CoachId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }

    }
}
