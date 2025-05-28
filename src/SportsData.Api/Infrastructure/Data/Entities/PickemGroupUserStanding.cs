using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Api.Infrastructure.Data.Entities
{
    public class PickemGroupUserStanding : CanonicalEntityBase<Guid>
    {
        public Guid PickemGroupId { get; set; }

        public Guid UserId { get; set; }

        public int SeasonYear { get; set; }

        public int SeasonWeek { get; set; }

        public int TotalPoints { get; set; }

        public int CorrectPicks { get; set; }

        public int TotalPicks { get; set; }

        public int WeeksWon { get; set; }

        public int Rank { get; set; } // Optional: calculated during insert

        public DateTime CalculatedUtc { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<PickemGroupUserStanding>
        {
            public void Configure(EntityTypeBuilder<PickemGroupUserStanding> builder)
            {
                builder.ToTable("LeagueStandingHistory");
                builder.HasKey(x => x.Id);
                builder.HasIndex(x => new { PickemGroupId = x.PickemGroupId, x.UserId, x.SeasonYear, x.SeasonWeek })
                    .IsUnique();
            }
        }
    }
}
