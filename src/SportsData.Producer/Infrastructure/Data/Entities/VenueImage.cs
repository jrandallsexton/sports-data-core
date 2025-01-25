﻿using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class VenueImage : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid VenueId {  get; set; }

        public int OriginalUrlHash { get; set; }

        public required string Url { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<VenueImage>
        {
            public void Configure(EntityTypeBuilder<VenueImage> builder)
            {
                builder.ToTable("VenueImage");
                builder.HasKey(t => t.Id);
                builder.HasOne<Venue>()
                    .WithMany(x => x.Images)
                    .HasForeignKey(x => x.VenueId);
                builder.HasIndex(x => x.OriginalUrlHash);
            }
        }
    }
}
