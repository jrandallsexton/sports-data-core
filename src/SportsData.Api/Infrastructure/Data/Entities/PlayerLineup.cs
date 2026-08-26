using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// One user's Player Pick'em roster for one league-week. Slots carry
    /// the athletes; locking is DERIVED per slot from the athlete's game
    /// start (see PlayerLineupSlot) — a lineup itself never locks. Created
    /// either by the first slot write of the week or by the lazy carry-over
    /// clone when the user first reads a week that follows a populated one.
    /// See docs/features/player-pickem/roster-persistence.md.
    /// </summary>
    public class PlayerLineup : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public PickemGroup Group { get; set; } = null!;

        public Guid UserId { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeek { get; set; }

        public ICollection<PlayerLineupSlot> Slots { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<PlayerLineup>
        {
            public void Configure(EntityTypeBuilder<PlayerLineup> builder)
            {
                builder.ToTable(nameof(PlayerLineup));
                builder.HasKey(x => x.Id);

                // One lineup per user per league-week.
                builder.HasIndex(x => new { x.PickemGroupId, x.UserId, x.SeasonYear, x.SeasonWeek })
                    .IsUnique();

                builder.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.PickemGroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Slots)
                    .WithOne(x => x.Lineup)
                    .HasForeignKey(x => x.PlayerLineupId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
