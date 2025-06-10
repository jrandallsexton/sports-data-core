using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

using System.ComponentModel.DataAnnotations;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class User : CanonicalEntityBase<Guid>
    {
        [Required]
        public required string FirebaseUid { get; set; }

        [EmailAddress]
        public required string Email { get; set; }

        public bool EmailVerified { get; set; }

        public required string SignInProvider { get; set; } = "unknown";

        public DateTime LastLoginUtc { get; set; }

        public string? DisplayName { get; set; }

        public string? Timezone { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<User>
        {
            public void Configure(EntityTypeBuilder<User> builder)
            {
                builder.ToTable("User");
                builder.HasKey(u => u.Id);

                builder.Property(u => u.FirebaseUid)
                    .IsRequired()
                    .HasMaxLength(128); // UID length limit from Firebase docs

                builder.Property(u => u.Email)
                    .IsRequired()
                    .HasMaxLength(256); // Standard email length cap

                builder.Property(u => u.SignInProvider)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(u => u.DisplayName)
                    .HasMaxLength(100);

                builder.Property(u => u.Timezone)
                    .HasMaxLength(100);
            }
        }

    }
}
