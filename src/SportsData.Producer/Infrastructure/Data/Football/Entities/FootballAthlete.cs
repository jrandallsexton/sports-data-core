using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities
{
    public class FootballAthlete : TeamAthleteBase
    {
        // Throwing hand (e.g. a QB's handedness). Baseball keeps Bats/Throws on
        // its athlete; this is the football analogue. Previously dropped by the
        // mapper. See docs/features/espn-processor-data-capture-audit.md.
        public string? HandType { get; set; }

        public string? HandAbbreviation { get; set; }

        public string? HandDisplayValue { get; set; }

        public new class EntityConfiguration : IEntityTypeConfiguration<FootballAthlete>
        {
            public void Configure(EntityTypeBuilder<FootballAthlete> builder)
            {
                builder.Property(x => x.HandType).HasMaxLength(20);
                builder.Property(x => x.HandAbbreviation).HasMaxLength(10);
                builder.Property(x => x.HandDisplayValue).HasMaxLength(20);
            }
        }
    }
}
