using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Api.Application;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupUser : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public Guid UserId { get; set; }

        public LeagueRole Role { get; set; } = LeagueRole.Member;

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupUser>
        {
            public void Configure(EntityTypeBuilder<PickemGroupUser> builder)
            {
                builder.ToTable(nameof(PickemGroupUser));
                builder.HasKey(x => x.Id);
                builder.HasIndex(x => new { x.PickemGroupId, x.UserId }).IsUnique();
            }
        }
    }
}
