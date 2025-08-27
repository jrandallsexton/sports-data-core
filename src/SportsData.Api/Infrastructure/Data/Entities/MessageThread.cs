using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class MessageThread : CanonicalEntityBase<Guid>
    {
        public Guid GroupId { get; set; }

        public User? User { get; set; }

        public DateTime LastActivityAt { get; set; }

        public string? Title { get; set; }

        public string? Slug { get; set; }

        public int PostCount { get; set; }

        public bool IsLocked { get; set; }

        public bool IsPinned { get; set; }

        public ICollection<MessagePost> Posts { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<MessageThread>
        {
            public void Configure(EntityTypeBuilder<MessageThread> b)
            {
                b.ToTable(nameof(MessageThread));
                b.HasKey(x => x.Id);
                b.HasIndex(x => new { x.GroupId, x.LastActivityAt });
                b.Property(x => x.Slug).HasMaxLength(128);

                b.HasOne<User>()      // or just `User` if no alias
                    .WithMany()
                    .HasForeignKey(x => x.CreatedBy)
                    .OnDelete(DeleteBehavior.Restrict); // or Cascade if you want to delete posts with users
            }
        }
    }
}
