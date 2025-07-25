using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Award : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public required string Name { get; set; }

        public string? Description { get; set; }

        public string? History { get; set; }

        public ICollection<AwardExternalId> ExternalIds { get; set; } = new List<AwardExternalId>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public ICollection<FranchiseSeasonAward> FranchiseSeasonAwards { get; set; } = new List<FranchiseSeasonAward>();

        public class EntityConfiguration : IEntityTypeConfiguration<Award>
        {
            public void Configure(EntityTypeBuilder<Award> builder)
            {
                builder.ToTable(nameof(Award));
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Name).IsRequired().HasMaxLength(200);
                builder.Property(x => x.Description).HasMaxLength(1000);
                builder.Property(x => x.History).HasMaxLength(2000);
            }
        }
    }
}
