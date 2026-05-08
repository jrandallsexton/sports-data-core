using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities
{
    // Football (NCAA + NFL) competitor row. Carries CuratedRankCurrent
    // (NCAA AP/Coaches/CFP rank snapshot at fetch time) — moved off the
    // shared base because MLB has no analogue.
    //
    // See docs/competition-competitor-split.md.
    public class FootballCompetitionCompetitor : CompetitionCompetitorBase
    {
        public int? CuratedRankCurrent { get; set; }

        public new class EntityConfiguration : IEntityTypeConfiguration<FootballCompetitionCompetitor>
        {
            public void Configure(EntityTypeBuilder<FootballCompetitionCompetitor> builder)
            {
                // CuratedRankCurrent (int?) is mapped by convention. No
                // explicit overrides needed; this method exists as a hook
                // for future football-only schema constraints.
            }
        }
    }
}
