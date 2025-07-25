﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Franchise : CanonicalEntityBase<Guid>, IHasSlug, IHasExternalIds
    {
        public Sport Sport { get; set; }

        public required string Name { get; set; }

        public string? Nickname { get; set; }

        public string? Abbreviation { get; set; }

        public required string Location { get; set; }

        public required string DisplayName { get; set; }

        public required string DisplayNameShort { get; set; }

        public required string ColorCodeHex { get; set; }

        public string? ColorCodeAltHex { get; set; }

        public bool IsActive { get; set; }

        public required string Slug { get; set; }

        public Guid VenueId { get; set; }

        public ICollection<FranchiseLogo> Logos { get; set; } = new List<FranchiseLogo>();

        public ICollection<FranchiseSeason> Seasons { get; set; } = new List<FranchiseSeason>();

        public ICollection<FranchiseExternalId> ExternalIds { get; set; } = new List<FranchiseExternalId>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<Franchise>
        {
            public void Configure(EntityTypeBuilder<Franchise> builder)
            {
                builder.ToTable(nameof(Franchise));
                builder.HasKey(t => t.Id);

                builder.Property(t => t.Name).HasMaxLength(100).IsRequired();
                builder.Property(t => t.Nickname).HasMaxLength(50);
                builder.Property(t => t.Abbreviation).HasMaxLength(10);
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