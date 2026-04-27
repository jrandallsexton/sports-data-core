using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    // Abstract base for sport-specific status entities. Mirrors the
    // CompetitionBase / FootballCompetition / BaseballCompetition split:
    // shared fields stay here; sport-specific fields and child collections
    // live on FootballCompetitionStatus / BaseballCompetitionStatus.
    //
    // Naming: matches the *Base convention used elsewhere in this
    // codebase (CompetitionBase, ContestBase, CompetitionPlayBase,
    // AthleteBase) so the abstract role is obvious at a glance.
    //
    // The Status nav was previously hung off CompetitionBase, which
    // pushed sport-specific concerns into the shared contract. It now
    // lives on each sport's Competition subclass typed to that sport's
    // CompetitionStatus subclass; the FK config moves with it.
    public abstract class CompetitionStatusBase : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid CompetitionId { get; set; }

        public double Clock { get; set; }

        public string DisplayClock { get; set; } = string.Empty;

        public int Period { get; set; }

        public string StatusTypeId { get; set; } = string.Empty;

        public string StatusTypeName { get; set; } = string.Empty;

        public string StatusState { get; set; } = string.Empty;

        public bool IsCompleted { get; set; }

        public string StatusDescription { get; set; } = string.Empty;

        public string StatusDetail { get; set; } = string.Empty;

        public string StatusShortDetail { get; set; } = string.Empty;

        public ICollection<CompetitionStatusExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionStatusBase>
        {
            public void Configure(EntityTypeBuilder<CompetitionStatusBase> builder)
            {
                // Literal table name (not nameof) preserves the existing
                // "CompetitionStatus" schema across this rename — every
                // prior migration references that string.
                builder.ToTable("CompetitionStatus");
                builder.HasKey(x => x.Id);

                builder.Property(x => x.Clock).IsRequired();
                builder.Property(x => x.DisplayClock).HasMaxLength(20);
                builder.Property(x => x.Period).IsRequired();

                builder.Property(x => x.StatusTypeId).HasMaxLength(10);
                builder.Property(x => x.StatusTypeName).HasMaxLength(50);
                builder.Property(x => x.StatusState).HasMaxLength(20);
                builder.Property(x => x.StatusDescription).HasMaxLength(100);
                builder.Property(x => x.StatusDetail).HasMaxLength(100);
                builder.Property(x => x.StatusShortDetail).HasMaxLength(50);

                builder.Property(x => x.IsCompleted).IsRequired();

                // Competition <-> Status FK is configured on each sport's
                // FootballCompetition / BaseballCompetition EntityConfiguration
                // so the relationship is typed to that sport's subclass.

                builder.HasMany(x => x.ExternalIds)
                    .WithOne(x => x.CompetitionStatus)
                    .HasForeignKey(x => x.CompetitionStatusId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
