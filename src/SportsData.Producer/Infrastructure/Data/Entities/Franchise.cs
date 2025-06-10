using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Franchise : CanonicalEntityBase<Guid>, IHasSlug
    {
        public Sport Sport { get; set; }

        public required string Name { get; set; }

        public required string Nickname { get; set; }

        public required string Abbreviation { get; set; }

        public required string Location { get; set; }

        public required string DisplayName { get; set; }

        public required string DisplayNameShort { get; set; }

        public required string ColorCodeHex { get; set; }

        public string? ColorCodeAltHex { get; set; }

        public bool IsActive { get; set; }

        public required string Slug { get; set; }

        public Guid VenueId { get; set; }

        public List<FranchiseLogo> Logos { get; set; } = [];

        public List<FranchiseSeason> Seasons { get; set; } = [];

        public List<FranchiseExternalId> ExternalIds { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Franchise>
        {
            public void Configure(EntityTypeBuilder<Franchise> builder)
            {
                builder.ToTable("Franchise");
                builder.HasKey(t => t.Id);

                builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
                builder.Property(t => t.Nickname).HasMaxLength(50).IsRequired();
                builder.Property(t => t.Abbreviation).HasMaxLength(10).IsRequired();
                builder.Property(t => t.Location).HasMaxLength(100).IsRequired();
                builder.Property(t => t.DisplayName).HasMaxLength(150).IsRequired();
                builder.Property(t => t.DisplayNameShort).HasMaxLength(100).IsRequired();
                builder.Property(t => t.ColorCodeHex).HasMaxLength(7).IsRequired(); // e.g. #FFFFFF
                builder.Property(t => t.ColorCodeAltHex).HasMaxLength(7);
                builder.Property(t => t.Slug).HasMaxLength(100).IsRequired();
            }
        }

    }
}