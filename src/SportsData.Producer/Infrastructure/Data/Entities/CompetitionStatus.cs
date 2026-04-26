using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionStatus : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid CompetitionId { get; set; }

        public CompetitionBase Competition { get; set; } = null!;

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

        // MLB-only: half-inning indicator (1 = top, 2 = bottom). Null for
        // sports that don't carry it. The shared CompetitionStatus table
        // hosts these baseball-specific fields rather than splitting the
        // hierarchy — football rows simply leave them null.
        public int? HalfInning { get; set; }

        // MLB-only: period prefix (e.g., "Top", "Bot", "End"). Same rationale
        // as HalfInning — nullable shared column, football leaves it null.
        public string? PeriodPrefix { get; set; }

        // MLB-only: featured at-bat athletes (current pitcher, batter, etc.)
        // tied to this status snapshot. Hard-replaced on every status update
        // by BaseballEventCompetitionStatusDocumentProcessor.
        public ICollection<CompetitionStatusFeaturedAthlete> FeaturedAthletes { get; set; } = [];

        public ICollection<CompetitionStatusExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionStatus>
        {
            public void Configure(EntityTypeBuilder<CompetitionStatus> builder)
            {
                builder.ToTable(nameof(CompetitionStatus));
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

                builder.Property(x => x.PeriodPrefix).HasMaxLength(10);

                builder.HasMany(x => x.FeaturedAthletes)
                    .WithOne(x => x.CompetitionStatus)
                    .HasForeignKey(x => x.CompetitionStatusId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Competition)
                    .WithOne(x => x.Status)
                    .HasForeignKey<CompetitionStatus>(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.ExternalIds)
                    .WithOne(x => x.CompetitionStatus)
                    .HasForeignKey(x => x.CompetitionStatusId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}