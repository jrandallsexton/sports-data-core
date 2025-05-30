﻿using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonLogo : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid FranchiseSeasonId { get; set; }

        public string OriginalUrlHash { get; set; }

        public string Url { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonLogo>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonLogo> builder)
            {
                builder.ToTable("FranchiseSeasonLogo");
                builder.HasKey(t => t.Id);
                builder.HasOne<FranchiseSeason>()
                    .WithMany(x => x.Logos)
                    .HasForeignKey(x => x.FranchiseSeasonId);
            }
        }
    }
}
