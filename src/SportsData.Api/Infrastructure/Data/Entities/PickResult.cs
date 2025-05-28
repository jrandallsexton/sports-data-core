using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickResult : CanonicalEntityBase<Guid>
    {
        public Guid UserPickId { get; set; }

        public bool IsCorrect { get; set; }

        public int PointsAwarded { get; set; }

        public string RuleVersion { get; set; } = "1.0";

        public class EntityConfiguration : IEntityTypeConfiguration<PickResult>
        {
            public void Configure(EntityTypeBuilder<PickResult> builder)
            {
                builder.ToTable("PickResult");
                builder.HasKey(x => x.Id);
                builder.HasIndex(x => x.UserPickId).IsUnique();
            }
        }
    }

}
