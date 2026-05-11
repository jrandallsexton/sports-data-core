using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Baseball.Entities;

/// <summary>
/// Baseball participant row. Backed by the shared `CompetitionPlayParticipant`
/// TPH table (configured on `CompetitionPlayParticipantBase`); the
/// auto-generated discriminator distinguishes baseball rows from a future
/// FootballCompetitionPlayParticipant. No baseball-specific fields today —
/// the subclass exists so participant taxonomy can diverge cleanly per
/// sport when needed (matches the BaseballCompetitionPlay pattern), and
/// because the FK navigation back to the play is sport-specific (FK
/// configured here, not on the abstract base, so a sport without
/// participants doesn't drag the base into its model).
/// </summary>
public class BaseballCompetitionPlayParticipant : CompetitionPlayParticipantBase
{
    public BaseballCompetitionPlay CompetitionPlay { get; set; } = null!;

    public new class EntityConfiguration : IEntityTypeConfiguration<BaseballCompetitionPlayParticipant>
    {
        public void Configure(EntityTypeBuilder<BaseballCompetitionPlayParticipant> builder)
        {
            builder.HasOne(t => t.CompetitionPlay)
                .WithMany(p => p.Participants)
                .HasForeignKey(t => t.CompetitionPlayId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
