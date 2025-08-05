using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupConference : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public PickemGroup PickemGroup { get; set; } = null!;

        /// <summary>
        /// CanonicalId of the Conference
        /// </summary>
        public Guid ConferenceId { get; set; }

        public required string ConferenceSlug { get; set; }
    }

    public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupConference>
    {
        public void Configure(EntityTypeBuilder<PickemGroupConference> builder)
        {
            builder.ToTable("PickemGroupConference");

            builder.HasKey(x => x.Id);

            builder
                .HasOne(x => x.PickemGroup)
                .WithMany(x => x.Conferences)
                .HasForeignKey(x => x.PickemGroupId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PickemGroupConference_PickemGroup");

            builder
                .Property(x => x.ConferenceId)
                .IsRequired();

            builder
                .Property(x => x.ConferenceSlug)
                .IsRequired()
                .HasMaxLength(100);

            // Optional: enforce uniqueness for each (Group, Conference) combo
            // builder.HasIndex(x => new { x.PickemGroupId, x.ConferenceId }).IsUnique();
        }
    }
}
