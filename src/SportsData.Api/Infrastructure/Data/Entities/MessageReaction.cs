using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class MessageReaction : CanonicalEntityBase<Guid>
    {
        public Guid PostId { get; set; }

        public Guid UserId { get; set; }

        public ReactionType Type { get; set; }

        public MessagePost Post { get; set; } = default!;

        public class EntityConfiguration : IEntityTypeConfiguration<MessageReaction>
        {
            public void Configure(EntityTypeBuilder<MessageReaction> b)
            {
                b.ToTable(nameof(MessageReaction));

                // Enforce one-per-(post,user)
                b.HasIndex(x => new { x.PostId, x.UserId }).IsUnique();

                // Fast counts / filtering
                b.HasIndex(x => new { x.PostId, x.Type });
                b.HasIndex(x => x.UserId);

                // Store enum as smallint
                b.Property(x => x.Type)
                    .HasConversion(v => (short)v, v => (ReactionType)v);

                // Cascade when a post is deleted
                b.HasOne(x => x.Post)
                    .WithMany(p => p.Reactions)
                    .HasForeignKey(x => x.PostId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }

    public enum ReactionType : short
    {
        Like = 1,       // 👍
        Dislike = 2,    // 👎
        Laugh = 3,      // 😂
        Sad = 4,        // 😢
        Angry = 5,      // 😡
        Surprise = 6    // 😮
    }

    public static class ReactionTypeExtensions
    {
        public static string ToIconClass(this ReactionType type) => type switch
        {
            ReactionType.Like => "fa-thumbs-up",
            ReactionType.Dislike => "fa-thumbs-down",
            ReactionType.Laugh => "fa-face-laugh",
            ReactionType.Sad => "fa-face-sad-tear",
            ReactionType.Angry => "fa-face-angry",
            ReactionType.Surprise => "fa-face-surprise",
            _ => "fa-question"
        };
    }

}
