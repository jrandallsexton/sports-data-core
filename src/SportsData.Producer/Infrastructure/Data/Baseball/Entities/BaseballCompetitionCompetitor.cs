using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities
{
    // MLB-specific competitor row. Carries no extra columns yet; Probables
    // (probable starting pitcher and similar) land here in Phase 2 of the
    // competition-competitor split.
    //
    // See docs/competition-competitor-split.md.
    public class BaseballCompetitionCompetitor : CompetitionCompetitorBase
    {
        public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionCompetitor>
        {
            public void Configure(EntityTypeBuilder<BaseballCompetitionCompetitor> builder)
            {
                // No sport-specific columns yet; the base config (mapped under
                // CompetitionCompetitorBase.EntityConfiguration) handles the
                // shared columns and TPH discriminator.
            }
        }
    }
}
