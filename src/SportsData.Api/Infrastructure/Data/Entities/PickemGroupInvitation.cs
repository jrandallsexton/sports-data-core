using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupInvitation : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public PickemGroup Group { get; set; } = null!;

        public Guid InvitedByUserId { get; set; }

        public User InvitedByUser { get; set; } = null!;

        public bool IsRevoked { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupInvitation>
        {
            public void Configure(EntityTypeBuilder<PickemGroupInvitation> builder)
            {
                builder.ToTable("PickemGroupInvitations");

                builder.HasKey(x => x.Id);

                builder.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.PickemGroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.InvitedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.InvitedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}
