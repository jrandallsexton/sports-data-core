using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// A per-user league invitation. Created when a member invites a
    /// registered user (by username, or by email that matches an existing
    /// account). Powers the "Pending Invitations" card on the web + mobile
    /// home pages. Accept delegates to the join path (so every join-policy
    /// gate applies); decline just stamps. A row is "pending" while
    /// AcceptedUtc, DeclinedUtc, and IsRevoked are all unset.
    /// </summary>
    public class PickemGroupInvitation : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public PickemGroup Group { get; set; } = null!;

        public Guid InvitedByUserId { get; set; }

        public User InvitedByUser { get; set; } = null!;

        /// <summary>The invited user. Invitations are only persisted for
        /// registered users — unregistered email invites remain email-only
        /// until the recipient creates an account.</summary>
        public Guid InviteeUserId { get; set; }

        public User InviteeUser { get; set; } = null!;

        public DateTime? AcceptedUtc { get; set; }

        public DateTime? DeclinedUtc { get; set; }

        public bool IsRevoked { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupInvitation>
        {
            public void Configure(EntityTypeBuilder<PickemGroupInvitation> builder)
            {
                builder.ToTable("PickemGroupInvitations");

                builder.HasKey(x => x.Id);

                builder.HasOne(x => x.InvitedByUser)
                    .WithMany()
                    .HasForeignKey(x => x.InvitedByUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasOne(x => x.InviteeUser)
                    .WithMany()
                    .HasForeignKey(x => x.InviteeUserId)
                    .OnDelete(DeleteBehavior.Restrict);

                // Pending-invitations lookup for the home cards.
                builder.HasIndex(x => x.InviteeUserId);

                // One PENDING invitation per (league, invitee), enforced in
                // the DB — the app-level dedupe check in
                // PendingInvitationWriter can race under concurrent invites;
                // a lost race surfaces as DbUpdateException instead of a
                // duplicate home-card row. Partial: accepted / declined /
                // revoked rows don't count, so re-inviting after a decline
                // stays legal.
                builder.HasIndex(x => new { x.PickemGroupId, x.InviteeUserId })
                    .IsUnique()
                    .HasFilter("\"AcceptedUtc\" IS NULL AND \"DeclinedUtc\" IS NULL AND NOT \"IsRevoked\"");
            }
        }
    }
}
