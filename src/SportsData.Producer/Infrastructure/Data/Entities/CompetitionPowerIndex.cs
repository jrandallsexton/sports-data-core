using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionPowerIndex : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid PowerIndexId { get; set; }

        public PowerIndex PowerIndex { get; set; } = null!;

        public Guid CompetitionId { get; set; }

        public Competition Competition { get; set; } = null!;

        public required Guid FranchiseSeasonId { get; set; }

        public FranchiseSeason FranchiseSeason { get; set; } = null!;

        public required double Value { get; set; }

        public required string DisplayValue { get; set; }

        public ICollection<CompetitionPowerIndexExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionPowerIndex>
        {
            public void Configure(EntityTypeBuilder<CompetitionPowerIndex> builder)
            {
                builder.ToTable(nameof(CompetitionPowerIndex));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Value)
                    .HasPrecision(18, 6);

                builder.Property(x => x.DisplayValue)
                    .IsRequired()
                    .HasMaxLength(64);

                builder.HasOne(x => x.PowerIndex)
                    .WithMany()
                    .HasForeignKey(x => x.PowerIndexId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasOne(x => x.Competition)
                    .WithMany(x => x.PowerIndexes)
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.FranchiseSeason)
                    .WithMany()
                    .HasForeignKey(x => x.FranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}