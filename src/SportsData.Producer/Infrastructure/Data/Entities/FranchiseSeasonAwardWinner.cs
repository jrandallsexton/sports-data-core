using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;
using System;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonAwardWinner : CanonicalEntityBase<Guid>
    {
        public Guid FranchiseSeasonAwardId { get; set; }
        public FranchiseSeasonAward FranchiseSeasonAward { get; set; } = null!;

        public string? AthleteRef { get; set; }
        public string? TeamRef { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonAwardWinner>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonAwardWinner> builder)
            {
                builder.ToTable(nameof(FranchiseSeasonAwardWinner));
                builder.HasKey(x => x.Id);
                builder.Property(x => x.AthleteRef).HasMaxLength(200);
                builder.Property(x => x.TeamRef).HasMaxLength(200);
                builder.HasOne(x => x.FranchiseSeasonAward)
                    .WithMany(x => x.Winners)
                    .HasForeignKey(x => x.FranchiseSeasonAwardId);
            }
        }
    }
}
