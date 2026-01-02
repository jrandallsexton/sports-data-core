using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Api.Application.Common.Enums;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupMember : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public PickemGroup Group { get; set; } = null!;

        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public LeagueRole Role { get; set; } = LeagueRole.Member;

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupMember>
        {
            public void Configure(EntityTypeBuilder<PickemGroupMember> builder)
            {
                builder.ToTable(nameof(PickemGroupMember));

                builder.HasKey(x => x.Id);

                builder.HasIndex(x => new { x.PickemGroupId, x.UserId }).IsUnique();

                builder
                    .HasOne(x => x.Group)
                    .WithMany(x => x.Members)
                    .HasForeignKey(x => x.PickemGroupId);

                builder
                    .HasOne(x => x.User)
                    .WithMany(x => x.GroupMemberships)
                    .HasForeignKey(x => x.UserId);

            }
        }
    }
}
