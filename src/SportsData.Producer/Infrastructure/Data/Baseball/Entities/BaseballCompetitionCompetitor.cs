using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities
{
    // MLB-specific competitor row. Owns the Probables collection (probable
    // starting pitcher today; ESPN's array shape allows future roles).
    //
    // See docs/competition-competitor-split.md and
    // docs/competition-competitor-probables.md.
    public class BaseballCompetitionCompetitor : CompetitionCompetitorBase
    {
        public ICollection<CompetitionCompetitorProbable> Probables { get; set; } = [];

        public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionCompetitor>
        {
            public void Configure(EntityTypeBuilder<BaseballCompetitionCompetitor> builder)
            {
                // Probables relationship is owned by
                // CompetitionCompetitorProbable.EntityConfiguration; this
                // hook stays for future baseball-only schema.
            }
        }
    }
}
