using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Play : CanonicalEntityBase<Guid>
    {
        public Competition Competition { get; set; } = null!; // Navigation property to Competition

        public Guid CompetitionId { get; set; } // FK to Competition

        public Drive? Drive { get; set; } // Navigation property to Drive

        public Guid? DriveId { get; set; } // FK to ContestDrive

        public required string EspnId { get; set; } // Maps to "id" in JSON

        public required string SequenceNumber { get; set; }

        public required PlayType Type { get; set; }

        public required string TypeId { get; set; }

        public required string Text { get; set; }

        public string? ShortText { get; set; }

        public string? AlternativeText { get; set; }

        public string? ShortAlternativeText { get; set; }

        public int AwayScore { get; set; }

        public int HomeScore { get; set; }

        public int PeriodNumber { get; set; }

        public double ClockValue { get; set; }

        public string? ClockDisplayValue { get; set; }

        public bool ScoringPlay { get; set; }

        public bool Priority { get; set; }

        public int ScoreValue { get; set; }

        public DateTime Modified { get; set; }

        public Guid TeamFranchiseSeasonId { get; set; } // FK to FranchiseSeason

        public int? StartDown { get; set; }

        public int? StartDistance { get; set; }

        public int? StartYardLine { get; set; }

        public int? StartYardsToEndzone { get; set; }

        public Guid? StartTeamFranchiseSeasonId { get; set; }

        public int? EndDown { get; set; }

        public int? EndDistance { get; set; }

        public int? EndYardLine { get; set; }

        public int? EndYardsToEndzone { get; set; }

        public int StatYardage { get; set; }

        public ICollection<PlayExternalId> ExternalIds { get; set; } = new List<PlayExternalId>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<Play>
        {
            public void Configure(EntityTypeBuilder<Play> builder)
            {
                builder.ToTable(nameof(Play));
                builder.HasKey(x => x.Id);
                builder.Property(x => x.EspnId).IsRequired().HasMaxLength(30);
                builder.Property(x => x.SequenceNumber).IsRequired().HasMaxLength(20);
                builder.Property(x => x.TypeId).IsRequired().HasMaxLength(10);
                builder.Property(x => x.Text).IsRequired().HasMaxLength(250);
                builder.Property(x => x.ShortText).HasMaxLength(250);
                builder.Property(x => x.AlternativeText).HasMaxLength(250);
                builder.Property(x => x.ShortAlternativeText).HasMaxLength(250);
                builder.Property(x => x.ClockDisplayValue).HasMaxLength(20);
                builder.Property(x => x.Modified).IsRequired();
                builder.Property(x => x.TeamFranchiseSeasonId).IsRequired();
                builder.Property(x => x.DriveId).IsRequired();
            }
        }
    }
}
