using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Person : CanonicalEntityBase<Guid>
    {
        public required string LastName { get; set; }

        public required string FirstName { get; set; }

        public string? Title { get; set; }

        public string? Nickname { get; set; }

        public int Experience { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Person>
        {
            public void Configure(EntityTypeBuilder<Person> builder)
            {
                builder.ToTable(nameof(Person));
                builder.HasKey(t => t.Id);
                builder.Property(t => t.LastName).IsRequired().HasMaxLength(100);
                builder.Property(t => t.FirstName).IsRequired().HasMaxLength(100);
                builder.Property(t => t.Title).HasMaxLength(100);
                builder.Property(t => t.Nickname).HasMaxLength(100);
            }
        }
    }
}
