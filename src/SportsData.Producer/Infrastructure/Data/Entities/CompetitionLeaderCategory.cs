using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionLeaderCategory : CanonicalEntityBase<int>
    {
        public required string Name { get; set; }

        public required string DisplayName { get; set; }

        public required string ShortDisplayName { get; set; }

        public required string Abbreviation { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionLeaderCategory>
        {
            public void Configure(EntityTypeBuilder<CompetitionLeaderCategory> builder)
            {
                builder.Property(c => c.Id)
                    .ValueGeneratedNever();

                builder.ToTable("lkLeaderCategory");

                builder.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(c => c.DisplayName)
                    .IsRequired()
                    .HasMaxLength(75);

                builder.Property(c => c.ShortDisplayName)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(c => c.Abbreviation)
                    .IsRequired()
                    .HasMaxLength(20);

                var now = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

                builder.HasData(
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.PassYards,
                        Name = "passingLeader",
                        DisplayName = "Passing Leader",
                        ShortDisplayName = "PASS",
                        Abbreviation = "PYDS",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.RushYards,
                        Name = "rushingLeader",
                        DisplayName = "Rushing Leader",
                        ShortDisplayName = "RUSH",
                        Abbreviation = "RYDS",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.RecYards,
                        Name = "receivingLeader",
                        DisplayName = "Receiving Leader",
                        ShortDisplayName = "REC",
                        Abbreviation = "RECYDS",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.PassingYards,
                        Name = "passingYards",
                        DisplayName = "Passing Yards",
                        ShortDisplayName = "PYDS",
                        Abbreviation = "YDS",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.RushingYards,
                        Name = "rushingYards",
                        DisplayName = "Rushing Yards",
                        ShortDisplayName = "RYDS",
                        Abbreviation = "YDS",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.ReceivingYards,
                        Name = "receivingYards",
                        DisplayName = "Receiving Yards",
                        ShortDisplayName = "RECYDS",
                        Abbreviation = "YDS",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.Tackles,
                        Name = "totalTackles",
                        DisplayName = "Tackles",
                        ShortDisplayName = "TACK",
                        Abbreviation = "TOT",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.Sacks,
                        Name = "sacks",
                        DisplayName = "Sacks",
                        ShortDisplayName = "SACK",
                        Abbreviation = "SACK",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.Interceptions,
                        Name = "interceptions",
                        DisplayName = "Interceptions",
                        ShortDisplayName = "INT",
                        Abbreviation = "INT",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.PuntReturns,
                        Name = "puntReturns",
                        DisplayName = "Punt Returns",
                        ShortDisplayName = "PR",
                        Abbreviation = "PR",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.KickReturns,
                        Name = "kickReturns",
                        DisplayName = "Kick Returns",
                        ShortDisplayName = "KR",
                        Abbreviation = "KR",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.Punts,
                        Name = "punts",
                        DisplayName = "Punts",
                        ShortDisplayName = "P",
                        Abbreviation = "P",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.KickingPoints,
                        Name = "totalKickingPoints",
                        DisplayName = "Kicking Points",
                        ShortDisplayName = "TP",
                        Abbreviation = "TP",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.Fumbles,
                        Name = "fumbles",
                        DisplayName = "Fumbles",
                        ShortDisplayName = "F",
                        Abbreviation = "F",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.FumblesLost,
                        Name = "fumblesLost",
                        DisplayName = "Fumbles Lost",
                        ShortDisplayName = "FL",
                        Abbreviation = "FL",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.FumblesRecovered,
                        Name = "fumblesRecovered",
                        DisplayName = "Fumbles Recovered",
                        ShortDisplayName = "CMP",
                        Abbreviation = "CMP",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.EspnRating,
                        Name = "espnRating",
                        DisplayName = "ESPN Rating Leader",
                        ShortDisplayName = "ESPNRating",
                        Abbreviation = "ESPNRating",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.PassTouchdowns,
                        Name = "passingTouchdowns",
                        DisplayName = "Passing Touchdowns",
                        ShortDisplayName = "TD",
                        Abbreviation = "TD",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.QbRating,
                        Name = "quarterbackRating",
                        DisplayName = "Quarterback Rating",
                        ShortDisplayName = "RAT",
                        Abbreviation = "RAT",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.RushTouchdowns,
                        Name = "rushingTouchdowns",
                        DisplayName = "Rushing Touchdowns",
                        ShortDisplayName = "TD",
                        Abbreviation = "TD",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.Receptions,
                        Name = "receptions",
                        DisplayName = "Receptions",
                        ShortDisplayName = "REC",
                        Abbreviation = "REC",
                        CreatedUtc = now
                    },
                    new CompetitionLeaderCategory
                    {
                        Id = (int)Core.Common.FootballLeaderCategory.RecTouchdowns,
                        Name = "receivingTouchdowns",
                        DisplayName = "Receiving Touchdowns",
                        ShortDisplayName = "TD",
                        Abbreviation = "TD",
                        CreatedUtc = now
                    }
                );
            }
        }
    }
}
