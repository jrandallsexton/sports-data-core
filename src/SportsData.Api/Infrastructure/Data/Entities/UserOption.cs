using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    /// <summary>
    /// Per-user option as a key/value row — deliberately EAV-shaped so new
    /// options are a code change (registry constant + DTO field), never a
    /// migration. The API projects known keys into the typed
    /// <c>UserOptionsDto</c>; unknown/stale keys are ignored on read and left
    /// untouched on write, keeping old and new clients forward-compatible.
    /// Known keys live in <c>UserOptionKeys</c>. A user with no row for a key
    /// gets that option's default. See docs/features/user-options.md.
    /// </summary>
    public class UserOption : CanonicalEntityBase<Guid>
    {
        public Guid UserId { get; set; }

        public User User { get; set; } = null!;

        public string Key { get; set; } = null!;

        public string Value { get; set; } = null!;

        public class EntityConfiguration : IEntityTypeConfiguration<UserOption>
        {
            public void Configure(EntityTypeBuilder<UserOption> builder)
            {
                builder.ToTable(nameof(UserOption));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Key).IsRequired().HasMaxLength(64);
                builder.Property(x => x.Value).IsRequired().HasMaxLength(256);

                // One row per (user, option).
                builder.HasIndex(x => new { x.UserId, x.Key }).IsUnique();

                builder.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
