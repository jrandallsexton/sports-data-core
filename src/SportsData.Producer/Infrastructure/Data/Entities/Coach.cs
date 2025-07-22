using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Coach : CanonicalEntityBase<Guid>
    {
        public required string LastName { get; set; }

        public required string FirstName { get; set; }

        public string? Title { get; set; }

        public string? Nickname { get; set; }

        public int Experience { get; set; }

        public ICollection<CoachSeason> Seasons { get; set; } = new List<CoachSeason>();

        public ICollection<CoachExternalId> ExternalIds { get; set; } = new List<CoachExternalId>();

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
            }
        }
    }
}
