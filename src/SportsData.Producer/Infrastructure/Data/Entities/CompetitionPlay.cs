using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionPlay : CanonicalEntityBase<Guid>, IHasExternalIds   
    {
        public Competition Competition { get; set; } = null!;

        public Guid CompetitionId { get; set; }

        public Drive? Drive { get; set; }

        public Guid? DriveId { get; set; }

        public required string EspnId { get; set; } // Maps to "id" in JSON

        /// <summary>
        /// Our canonical ordinal position of the play within the competition
        /// Generated via enrichment after the fact, not from source
        /// </summary>
        public int? Ordinal { get; set; }

        public required string SequenceNumber { get; set; } // this is from ESPN

        public PlayType Type { get; set; }

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

        public ICollection<CompetitionProbability> Probabilities { get; set; } = [];

        public ICollection<CompetitionPlayExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionPlay>
        {
            public void Configure(EntityTypeBuilder<CompetitionPlay> builder)
            {
                builder.ToTable(nameof(CompetitionPlay));
                builder.HasKey(x => x.Id);

                builder.Property(x => x.EspnId).IsRequired().HasMaxLength(30);
                builder.Property(x => x.SequenceNumber).IsRequired().HasMaxLength(20);
                builder.Property(x => x.TypeId).IsRequired().HasMaxLength(10);
                builder.Property(x => x.Text).IsRequired().HasMaxLength(500);
                builder.Property(x => x.ShortText).HasMaxLength(250);
                builder.Property(x => x.AlternativeText).HasMaxLength(500);
                builder.Property(x => x.ShortAlternativeText).HasMaxLength(250);
                builder.Property(x => x.ClockDisplayValue).HasMaxLength(20);
                builder.Property(x => x.Modified).IsRequired();
                builder.Property(x => x.TeamFranchiseSeasonId).IsRequired();
                builder.Property(x => x.DriveId).IsRequired(false); // Nullable FK
                builder.Property(x => x.CompetitionId).IsRequired();

                builder.Property(x => x.Type)
                    .IsRequired()
                    .HasConversion<int>();

                builder.HasOne(x => x.Drive)
                    .WithMany(x => x.Plays)
                    .HasForeignKey(x => x.DriveId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.Competition)
                    .WithMany(x => x.Plays)
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.ExternalIds)
                    .WithOne()
                    .HasForeignKey(x => x.CompetitionPlayId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.Probabilities)
                    .WithOne(x => x.Play)
                    .HasForeignKey(x => x.PlayId)
                    .OnDelete(DeleteBehavior.Restrict);
            }
        }
    }
}