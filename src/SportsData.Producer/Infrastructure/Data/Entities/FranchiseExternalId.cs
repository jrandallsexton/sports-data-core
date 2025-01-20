﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseExternalId : ExternalId
{
    public Franchise Franchise { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseExternalId>
    {
        public void Configure(EntityTypeBuilder<FranchiseExternalId> builder)
        {
            builder.ToTable("FranchiseExternalId");
            builder.HasKey(t => t.Id);
        }
    }
}