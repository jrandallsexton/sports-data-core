﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class MessagePost : CanonicalEntityBase<Guid>
    {
        public Guid ThreadId { get; set; }

        public Guid? ParentId { get; set; }

        public User User { get; set; } = null!;

        public int Depth { get; set; } // 0 = root

        public string Path { get; set; } = ""; // e.g., "0001.0003"

        public string Content { get; set; } = "";

        public DateTime? EditedAt { get; set; }

        public bool IsDeleted { get; set; }

        public int ReplyCount { get; set; }

        public int LikeCount { get; set; }

        public int DislikeCount { get; set; }

        public MessageThread Thread { get; set; } = default!;

        public MessagePost? Parent { get; set; }

        public ICollection<MessagePost> Children { get; set; } = [];

        public ICollection<MessageReaction> Reactions { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<MessagePost>
        {
            public void Configure(EntityTypeBuilder<MessagePost> b)
            {
                b.ToTable(nameof(MessagePost));
                b.HasKey(x => x.Id);
                b.HasIndex(x => new { x.ThreadId, x.Path });
                b.HasIndex(x => new { x.ThreadId, x.ParentId });

                b.Property(x => x.Path).HasMaxLength(1024);
                b.HasOne(x => x.Thread).WithMany(t => t.Posts).HasForeignKey(x => x.ThreadId);
                b.HasOne(x => x.Parent).WithMany(p => p.Children).HasForeignKey(x => x.ParentId);

                b.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.CreatedBy)
                    .IsRequired()
                    .OnDelete(DeleteBehavior.Restrict);

            }
        }
    }
}
