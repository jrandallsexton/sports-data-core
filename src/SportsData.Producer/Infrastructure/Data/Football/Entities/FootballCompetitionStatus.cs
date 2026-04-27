using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities;

// Sport-specific subclass for football status. No additional fields
// today — exists so the type system records "this is football's
// status" and the FootballCompetition.Status nav binds to it
// concretely. Future football-only signals (red-zone state, etc.)
// can land here without touching the base entity.
public class FootballCompetitionStatus : CompetitionStatusBase
{
    public new class EntityConfiguration : IEntityTypeConfiguration<FootballCompetitionStatus>
    {
        public void Configure(EntityTypeBuilder<FootballCompetitionStatus> builder)
        {
            // No subclass-specific configuration yet; declared so each
            // sport's DataContext registers a parallel hook point.
        }
    }
}
