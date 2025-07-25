using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class FranchiseExternalId : ExternalId
{
    public Guid FranchiseId { get; set; }

    public Franchise Franchise { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<FranchiseExternalId>
    {
        public void Configure(EntityTypeBuilder<FranchiseExternalId> builder)
        {
            builder.ToTable(nameof(FranchiseExternalId));
            builder.HasKey(t => t.Id);
            builder.HasOne(t => t.Franchise)
                   .WithMany(f => f.ExternalIds)
                   .HasForeignKey(t => t.FranchiseId);
        }
    }
}