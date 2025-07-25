﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class GroupLogo : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid GroupId { get; set; }

        public required string OriginalUrlHash { get; set; }

        public required Uri Uri { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<GroupLogo>
        {
            public void Configure(EntityTypeBuilder<GroupLogo> builder)
            {
                builder.ToTable(nameof(GroupLogo));
                builder.HasKey(t => t.Id);
                builder.HasOne<Group>()
                    .WithMany(x => x.Logos)
                    .HasForeignKey(x => x.GroupId);
                builder.HasIndex(x => x.OriginalUrlHash);
                builder.Property(x => x.OriginalUrlHash).HasMaxLength(64);
                builder.Property(x => x.Uri).HasMaxLength(256);
            }
        }
    }
}
