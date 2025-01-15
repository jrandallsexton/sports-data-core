using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Franchise : EntityBase<Guid>
    {
        public string Name { get; set; }

        public string Nickname { get; set; }

        public string Abbreviation { get; set; }

        public string DisplayName { get; set; }

        public string DisplayNameShort { get; set; }

        public string ColorCodeHex { get; set; }

        public bool IsActive { get; set; }

        public List<FranchiseLogo> Logos { get; set; } = [];

        public string Slug { get; set; }

        public List<ExternalId> ExternalIds { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Franchise>
        {
            public void Configure(EntityTypeBuilder<Franchise> builder)
            {
                builder.ToTable("Franchise");
                builder.HasKey(t => t.Id);
            }
        }
    }
}