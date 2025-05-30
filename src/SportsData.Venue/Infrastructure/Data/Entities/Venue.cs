﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Venue.Infrastructure.Data.Entities
{
    public class Venue : EntityBase<int>
    {
        public string Name { get; set; }

        public string ShortName { get; set; }

        public bool IsGrass { get; set; }

        public bool IsIndoor { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<Venue>
        {
            public void Configure(EntityTypeBuilder<Venue> builder)
            {
                builder.ToTable("Venue");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
