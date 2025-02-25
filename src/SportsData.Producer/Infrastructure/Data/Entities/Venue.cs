﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Venue : CanonicalEntityBase<Guid>
    {
        public string Name { get; set; }

        public string ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }

        public List<VenueExternalId> ExternalIds { get; set; }

        public List<VenueImage> Images { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Venue>
        {
            public void Configure(EntityTypeBuilder<Venue> builder)
            {
                builder.ToTable("Venue");
                //builder.UseTpcMappingStrategy();
                builder.HasKey(t => t.Id);
                builder.Property(p => p.Id).ValueGeneratedNever();
            }
        }
    }
}
