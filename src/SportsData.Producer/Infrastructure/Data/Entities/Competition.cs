using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Competition : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Contest Contest { get; set; } = null!;

        public Guid ContestId { get; set; } // FK to Contest

        public DateTime Date { get; set; }

        public int Attendance { get; set; }

        public bool TimeValid { get; set; }

        public bool DateValid { get; set; }

        public bool IsNeutralSite { get; set; }

        public bool IsDivisionCompetition { get; set; }

        public bool IsConferenceCompetition { get; set; }

        public bool IsPreviewAvailable { get; set; }

        public bool IsRecapAvailable { get; set; }

        public bool IsBoxscoreAvailable { get; set; }

        public bool IsLineupAvailable { get; set; }

        public bool IsGamecastAvailable { get; set; }

        public bool IsPlayByPlayAvailable { get; set; }

        public bool IsConversationAvailable { get; set; }

        public bool IsCommentaryAvailable { get; set; }

        public bool IsPickCenterAvailable { get; set; }

        public bool IsSummaryAvailable { get; set; }

        public bool IsLiveAvailable { get; set; }

        public bool IsTicketsAvailable { get; set; }

        public bool IsShotChartAvailable { get; set; }

        public bool IsTimeoutsAvailable { get; set; }

        public bool IsPossessionArrowAvailable { get; set; }

        public bool IsOnWatchEspn { get; set; }

        public bool IsRecent { get; set; }

        public bool IsBracketAvailable { get; set; }

        public bool IsWallClockAvailable { get; set; }

        public bool IsHighlightsAvailable { get; set; }

        public bool HasDefensiveStats { get; set; }

        public string? TypeId { get; set; }

        public string? TypeText { get; set; }

        public string? TypeAbbreviation { get; set; }

        public string? TypeSlug { get; set; }

        public string? TypeName { get; set; }

        public CompetitionSource? GameSource { get; set; }

        public CompetitionSource? BoxscoreSource { get; set; }

        public CompetitionSource? LinescoreSource { get; set; }

        public CompetitionSource? PlayByPlaySource { get; set; }

        public CompetitionSource? StatsSource { get; set; }

        public Guid? VenueId { get; set; } // FK to Venue

        public ICollection<Competitor> Competitors { get; set; } = new List<Competitor>();

        public ICollection<CompetitionNote> Notes { get; set; } = new List<CompetitionNote>();

        public ICollection<Play> Plays { get; set; } = new List<Play>();

        public ICollection<Drive> Drives { get; set; } = new List<Drive>();

        public string? FormatRegulationDisplayName { get; set; }

        public int? FormatRegulationPeriods { get; set; }

        public string? FormatRegulationSlug { get; set; }

        public double? FormatRegulationClock { get; set; }

        public string? FormatOvertimeDisplayName { get; set; }

        public int? FormatOvertimePeriods { get; set; }

        public string? FormatOvertimeSlug { get; set; }

        public ICollection<CompetitionLink> Links { get; set; } = new List<CompetitionLink>(); // Normalized set of rel/href for downstream use

        public ICollection<CompetitionExternalId> ExternalIds { get; set; } = new List<CompetitionExternalId>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<Competition>
        {
            public void Configure(EntityTypeBuilder<Competition> builder)
            {
                builder.ToTable(nameof(Competition));
                builder.HasKey(x => x.Id);
                builder.Property(x => x.TypeId).HasMaxLength(20);
                builder.Property(x => x.TypeText).HasMaxLength(50);
                builder.Property(x => x.TypeAbbreviation).HasMaxLength(20);
                builder.Property(x => x.TypeSlug).HasMaxLength(40);
                builder.Property(x => x.TypeName).HasMaxLength(40);
                builder.Property(x => x.FormatRegulationDisplayName).HasMaxLength(50);
                builder.Property(x => x.FormatRegulationSlug).HasMaxLength(40);
                builder.Property(x => x.FormatOvertimeDisplayName).HasMaxLength(50);
                builder.Property(x => x.FormatOvertimeSlug).HasMaxLength(40);

                builder.HasMany(x => x.Competitors)
                    .WithOne()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Notes)
                    .WithOne()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Contest)
                    .WithMany(x => x.Competitions)
                    .HasForeignKey(x => x.ContestId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.ExternalIds)
                    .WithOne()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder
                    .HasMany(x => x.Links)
                    .WithOne(x => x.Competition)
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
