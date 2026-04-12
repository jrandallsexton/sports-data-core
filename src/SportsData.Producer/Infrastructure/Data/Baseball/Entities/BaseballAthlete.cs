using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities
{
    public class BaseballAthlete : TeamAthleteBase
    {
        public string? BatsType { get; set; }

        public string? BatsAbbreviation { get; set; }

        public string? ThrowsType { get; set; }

        public string? ThrowsAbbreviation { get; set; }

        public new class EntityConfiguration : IEntityTypeConfiguration<BaseballAthlete>
        {
            public void Configure(EntityTypeBuilder<BaseballAthlete> builder)
            {
                builder.Property(x => x.BatsType).HasMaxLength(20);
                builder.Property(x => x.BatsAbbreviation).HasMaxLength(5);
                builder.Property(x => x.ThrowsType).HasMaxLength(20);
                builder.Property(x => x.ThrowsAbbreviation).HasMaxLength(5);
            }
        }
    }
}
