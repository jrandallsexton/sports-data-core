using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

using System.ComponentModel.DataAnnotations.Schema;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Contest : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public required string Name { get; set; }

        public required string ShortName { get; set; }

        public Guid HomeTeamFranchiseSeasonId { get; set; }

        public FranchiseSeason? HomeTeamFranchiseSeason { get; set; }

        public Guid AwayTeamFranchiseSeasonId { get; set; }

        public FranchiseSeason? AwayTeamFranchiseSeason { get; set; }

        public required DateTime StartDateUtc { get; set; }

        public DateTime? EndDateUtc { get; set; }

        public int Period { get; set; } = -1;

        public required Sport Sport { get; set; }

        public required int SeasonYear { get; set; }

        public int? Week { get; set; }               // From `week` ref, parsed from URL or hydrated from companion doc

        public Guid SeasonWeekId { get; set; }

        public SeasonWeek SeasonWeek { get; set; } = null!;

        public Guid SeasonPhaseId { get; set; }

        public string? EventNote { get; set; }       // e.g., "Modelo Vegas Kickoff Classic"

        public Venue? Venue { get; set; }

        public Guid? VenueId { get; set; }

        // === Scoring Results ===
        public int? HomeScore { get; set; }

        public int? AwayScore { get; set; }

        public Guid? WinnerFranchiseId { get; set; }           // Straight-up

        public Guid? SpreadWinnerFranchiseId { get; set; }     // ATS winner

        public OverUnderResult OverUnder { get; set; } = OverUnderResult.None;

        public DateTime? FinalizedUtc { get; set; }

        // === Helpers (not mapped to DB) ===
        [NotMapped]
        public bool IsFinal => FinalizedUtc.HasValue;

        [NotMapped]
        public int? TotalScore =>
            HomeScore.HasValue && AwayScore.HasValue
                ? HomeScore + AwayScore
                : null;

        public ICollection<ContestLink> Links { get; set; } = new List<ContestLink>(); // Normalized set of rel/href for downstream use
        
        public ICollection<ContestExternalId> ExternalIds { get; set; } = new List<ContestExternalId>();

        public ICollection<Competition> Competitions { get; set; } = new List<Competition>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<Contest>
        {
            public void Configure(EntityTypeBuilder<Contest> builder)
            {
                builder.ToTable(nameof(Contest));

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
                    .IsRequired(true);

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
