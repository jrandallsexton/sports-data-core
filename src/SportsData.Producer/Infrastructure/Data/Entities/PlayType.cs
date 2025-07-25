using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class PlayTypeCategory : CanonicalEntityBase<int>
    {
        public string Name { get; set; } = default!;

        public string Description { get; set; } = default!;

        public class EntityConfiguration : IEntityTypeConfiguration<PlayTypeCategory>
        {
            public void Configure(EntityTypeBuilder<PlayTypeCategory> builder)
            {
                builder.Property(c => c.Id)
                    .ValueGeneratedNever();

                builder.ToTable("lkPlayType");

                builder.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(c => c.Description)
                    .IsRequired()
                    .HasMaxLength(75);

                builder.HasData(
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.CoinToss,
                        Name = "coinToss",
                        Description = "Coin Toss",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.EndOfGame,
                        Name = "endOfGame",
                        Description = "End of Game",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.EndOfHalf,
                        Name = "endOfHalf",
                        Description = "End of Half",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.EndPeriod,
                        Name = "endPeriod",
                        Description = "End Period",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.FieldGoalGood,
                        Name = "fieldGoalGood",
                        Description = "Field Goal Good",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.FieldGoalMissed,
                        Name = "fieldGoalMissed",
                        Description = "Field Goal Missed",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.FumbleRecoveryOwn,
                        Name = "fumbleRecoveryOwn",
                        Description = "Fumble Recovery (Own)",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.Kickoff,
                        Name = "kickoff",
                        Description = "Kickoff",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.KickoffReturnOffense,
                        Name = "kickoffReturnOffense",
                        Description = "Kickoff Return (Offense)",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.PassIncompletion,
                        Name = "passIncompletion",
                        Description = "Pass Incompletion",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.PassInterceptionReturn,
                        Name = "passInterceptionReturn",
                        Description = "Pass Interception Return",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.PassReception,
                        Name = "passReception",
                        Description = "Pass Reception",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.PassingTouchdown,
                        Name = "passingTouchdown",
                        Description = "Passing Touchdown",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.Penalty,
                        Name = "penalty",
                        Description = "Penalty",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.Punt,
                        Name = "punt",
                        Description = "Punt",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.Rush,
                        Name = "rush",
                        Description = "Rush",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.RushingTouchdown,
                        Name = "rushingTouchdown",
                        Description = "Rushing Touchdown",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.Sack,
                        Name = "sack",
                        Description = "Sack",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new PlayTypeCategory
                    {
                        Id = (int)PlayType.Timeout,
                        Name = "timeout",
                        Description = "Timeout",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                );
            }
        }
    }
}
