using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Football.Entities;

/// <summary>
/// Football participant row. Backed by the shared `CompetitionPlayParticipant`
/// TPH table (configured on `CompetitionPlayParticipantBase`); the
/// auto-generated discriminator distinguishes football rows from the baseball
/// subclass. No football-specific fields today — the subclass exists so
/// participant taxonomy (passer / rusher / receiver / tackler / …) can diverge
/// cleanly per sport when needed, and because the FK navigation back to the
/// play is sport-specific (FK configured here, not on the abstract base, so a
/// sport without participants doesn't drag the base into its model). Mirrors
/// BaseballCompetitionPlayParticipant.
/// </summary>
public class FootballCompetitionPlayParticipant : CompetitionPlayParticipantBase
{
    public FootballCompetitionPlay CompetitionPlay { get; set; } = null!;

    public new class EntityConfiguration : IEntityTypeConfiguration<FootballCompetitionPlayParticipant>
    {
        public void Configure(EntityTypeBuilder<FootballCompetitionPlayParticipant> builder)
        {
            builder.HasOne(t => t.CompetitionPlay)
                .WithMany(p => p.Participants)
                .HasForeignKey(t => t.CompetitionPlayId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
