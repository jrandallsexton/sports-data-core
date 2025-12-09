using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupWeekResult : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public Guid UserId { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeek { get; set; }

        public int TotalPoints { get; set; }

        public int CorrectPicks { get; set; }

        public int TotalPicks { get; set; }

        public bool IsWeeklyWinner { get; set; }

        public bool IsDropWeek { get; set; }

        public int? Rank { get; set; }

        public DateTime CalculatedUtc { get; set; }

        // Navigation properties
        public PickemGroup? Group { get; set; }

        public User? User { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupWeekResult>
        {
            public void Configure(EntityTypeBuilder<PickemGroupWeekResult> builder)
            {
                builder.ToTable(nameof(PickemGroupWeekResult));

                builder.HasKey(x => x.Id);

                builder.HasIndex(x => new
                {
                    PickemGroupId = x.PickemGroupId,
                    x.SeasonYear,
                    x.SeasonWeek,
                    x.UserId
                }).IsUnique();

                builder.Property(x => x.TotalPoints).IsRequired();
                builder.Property(x => x.CorrectPicks).IsRequired();
                builder.Property(x => x.TotalPicks).IsRequired();
                builder.Property(x => x.IsWeeklyWinner).IsRequired();
                builder.Property(x => x.IsDropWeek).IsRequired().HasDefaultValue(false);
                builder.Property(x => x.Rank).IsRequired(false);
                builder.Property(x => x.CalculatedUtc).IsRequired();

                // Relationships
                builder.HasOne(x => x.Group)
                    .WithMany()
                    .HasForeignKey(x => x.PickemGroupId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.User)
                    .WithMany()
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}