using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

using System.ComponentModel.DataAnnotations.Schema;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public abstract class ContestBase : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public required string Name { get; set; }

        public required string ShortName { get; set; }

        public Guid HomeTeamFranchiseSeasonId { get; set; }

        public FranchiseSeason HomeTeamFranchiseSeason { get; set; } = null!;

        public Guid AwayTeamFranchiseSeasonId { get; set; }

        public FranchiseSeason AwayTeamFranchiseSeason { get; set; } = null!;

        public required DateTime StartDateUtc { get; set; }

        public DateTime? EndDateUtc { get; set; }

        public int Period { get; set; } = -1;

        public required Sport Sport { get; set; }

        public required int SeasonYear { get; set; }

        public int? Week { get; set; }               // From `week` ref, parsed from URL or hydrated from companion doc

        public Guid? SeasonWeekId { get; set; }

        public SeasonWeek? SeasonWeek { get; set; }

        public Guid SeasonPhaseId { get; set; }

        public string? EventNote { get; set; }       // e.g., "Modelo Vegas Kickoff Classic"

        public Venue? Venue { get; set; }

        public Guid? VenueId { get; set; }

        // === Scoring Results ===
        public int? HomeScore { get; set; }

        public int? AwayScore { get; set; }

        public Guid? WinnerFranchiseSeasonId { get; set; }           // Straight-up

        public Guid? SpreadWinnerFranchiseSeasonId { get; set; }     // ATS winner

        public OverUnderResult OverUnder { get; set; } = OverUnderResult.None;

        public DateTime? FinalizedUtc { get; set; }

        // === Cancellation ===
        // Stamped (once) by EventCompetitionStatusProcessorBase (the shared
        // lifecycle hook used by both Football + Baseball status-doc
        // processors) when ESPN reports StatusTypeName == "STATUS_CANCELED".
        // Treated as terminal/irrevocable — ContestEnrichmentJob excludes
        // these from future runs. See docs/contest-enrichment-historical-sweep.md.
        public DateTime? CancelledUtc { get; set; }

        // === Audit ===
        // Stamped by ContestEnrichmentAuditJob after it verifies that the
        // current Contest state (scores, winner) matches what re-running the
        // enrichment processor would produce now. Acts as an idempotency
        // flag so the nightly audit sweep does not re-check the same
        // finalized contest forever once it has been validated.
        // Cleared (null) means "needs audit" — either never audited, or
        // a prior audit found a mismatch that triggered re-enrichment.
        public DateTime? AuditedUtc { get; set; }

        // === Helpers (not mapped to DB) ===
        [NotMapped]
        public bool IsFinal => FinalizedUtc.HasValue;

        [NotMapped]
        public bool IsCancelled => CancelledUtc.HasValue;

        [NotMapped]
        public int? TotalScore =>
            HomeScore.HasValue && AwayScore.HasValue
                ? HomeScore + AwayScore
                : null;

        public ICollection<ContestLink> Links { get; set; } = new List<ContestLink>(); // Normalized set of rel/href for downstream use
        
        public ICollection<ContestExternalId> ExternalIds { get; set; } = new List<ContestExternalId>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<ContestBase>
        {
            public void Configure(EntityTypeBuilder<ContestBase> builder)
            {
                builder.ToTable("Contest");

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(x => x.ShortName)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(x => x.StartDateUtc)
                    .IsRequired();

                builder.Property(x => x.EndDateUtc);

                builder.Property(x => x.Sport)
                    .IsRequired();

                builder.Property(x => x.SeasonYear)
                    .IsRequired();

                // Backfill paths in ContestUpdateJob/ContestEnrichmentJob filter
                // by SeasonYear; index avoids a full Contest scan during runs.
                builder.HasIndex(x => x.SeasonYear);

                // Filtered index for ContestEnrichmentAuditJob's batch scan.
                // Steady-state, the audit candidate set is tiny (yesterday's
                // newly-finalized contests); the index keeps the nightly scan
                // O(candidates) instead of scanning the whole Contest table.
                builder.HasIndex(x => x.FinalizedUtc)
                    .HasFilter("\"FinalizedUtc\" IS NOT NULL AND \"AuditedUtc\" IS NULL")
                    .HasDatabaseName("IX_Contest_AuditedUtc_Pending");

                builder.Property(x => x.SeasonPhaseId);

                builder.Property(x => x.Week);

                builder.Property(x => x.EventNote)
                    .HasMaxLength(250);

                builder.Property(x => x.VenueId);
                builder
                    .HasOne(x => x.Venue)                    // 👈 Venue navigation
                    .WithMany()                             // 👈 no reverse nav from Venue to Contest
                    .HasForeignKey(x => x.VenueId)          // 👈 foreign key
                    .OnDelete(DeleteBehavior.Restrict);     // 👈 optional: prevent cascading deletes

                builder
                    .HasOne(x => x.HomeTeamFranchiseSeason)
                    .WithMany()
                    .HasForeignKey(x => x.HomeTeamFranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder
                    .HasOne(x => x.AwayTeamFranchiseSeason)
                    .WithMany()
                    .HasForeignKey(x => x.AwayTeamFranchiseSeasonId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder
                    .Property(x => x.SeasonWeekId)
                    .IsRequired(false);

                builder
                    .HasOne(x => x.SeasonWeek)
                    .WithMany()
                    .HasForeignKey(x => x.SeasonWeekId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder
                    .HasMany(x => x.Links)
                    .WithOne(x => x.Contest)
                    .HasForeignKey(x => x.ContestId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
