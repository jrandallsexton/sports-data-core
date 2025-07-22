using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseSeasonRecordAtsCategory : CanonicalEntityBase<int>
    {
        public string Name { get; set; } = default!;

        public string Description { get; set; } = default!;

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseSeasonRecordAtsCategory>
        {
            public void Configure(EntityTypeBuilder<FranchiseSeasonRecordAtsCategory> builder)
            {
                builder.Property(c => c.Id)
                    .ValueGeneratedNever();

                builder.ToTable("lkRecordAtsCategory");

                builder.Property(c => c.Name)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(c => c.Description)
                    .IsRequired()
                    .HasMaxLength(75);

                builder.HasData(
                    new FranchiseSeasonRecordAtsCategory
                    {
                        Id = (int)AtsCategory.AtsOverall,
                        Name = "atsOverall",
                        Description = "Overall team season record against the spread",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new FranchiseSeasonRecordAtsCategory
                    {
                        Id = (int)AtsCategory.AtsFavorite,
                        Name = "atsFavorite",
                        Description = "Team season record against the spread as the favorite",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new FranchiseSeasonRecordAtsCategory
                    {
                        Id = (int)AtsCategory.AtsUnderdog,
                        Name = "atsUnderdog",
                        Description = "Team season record against the spread as the underdog",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new FranchiseSeasonRecordAtsCategory
                    {
                        Id = (int)AtsCategory.AtsAway,
                        Name = "atsAway",
                        Description = "Team season record against the spread as the away team",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new FranchiseSeasonRecordAtsCategory
                    {
                        Id = (int)AtsCategory.AtsHome,
                        Name = "atsHome",
                        Description = "Team season record against the spread as the home team",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new FranchiseSeasonRecordAtsCategory
                    {
                        Id = (int)AtsCategory.AtsAwayFavorite,
                        Name = "atsAwayFavorite",
                        Description = "Team season record against the spread as the away favorite",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new FranchiseSeasonRecordAtsCategory
                    {
                        Id = (int)AtsCategory.AtsAwayUnderdog,
                        Name = "atsAwayUnderdog",
                        Description = "Team season record against the spread as the away underdog",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    },
                    new FranchiseSeasonRecordAtsCategory
                    {
                        Id = (int)AtsCategory.AtsHomeFavorite,
                        Name = "atsHomeFavorite",
                        Description = "Team season record against the spread as the home favorite",
                        CreatedUtc = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc)
                    }
                );
            }
        }
    }
}
