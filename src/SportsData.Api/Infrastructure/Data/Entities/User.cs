using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SportsData.Core.Infrastructure.Data.Entities;

using System.ComponentModel.DataAnnotations;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class User : CanonicalEntityBase<Guid>
    {
        [Required]
        public string FirebaseUid { get; set; }

        [EmailAddress]
        public string Email { get; set; }

        public bool EmailVerified { get; set; }

        public string SignInProvider { get; set; } = "unknown";

        public DateTime LastLoginUtc { get; set; }

        public string? DisplayName { get; set; }

        public string? Timezone { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<User>
        {
            public void Configure(EntityTypeBuilder<User> builder)
            {
                builder.ToTable("User");
                builder.HasKey(u => u.Id);
                builder.Property(u => u.Email).IsRequired().HasMaxLength(256);
                builder.Property(u => u.FirebaseUid).IsRequired();
            }
        }
    }
}
