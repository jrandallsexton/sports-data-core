using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Drive : CanonicalEntityBase<Guid>
    {
        public Competition Competition { get; set; } = null!;

        public Guid CompetitionId { get; set; }

        /// <summary>
        /// Example: "13 plays, 74 yards, 7:14"
        /// </summary>
        public required string Description { get; set; }

        /// <summary>
        /// string value of ordinal position of the drive
        /// </summary>
        public required string SequenceNumber { get; set; }

        /// <summary>
        /// integer value of ordinal position of the drive
        /// </summary>
        public required int Ordinal { get; set; }

        /// <summary>
        /// eg. "DOWNS"
        /// </summary>
        public string? Result { get; set; }

        /// <summary>
        /// eg. "DOWNS"
        /// </summary>
        public string? ShortDisplayResult { get; set; }

        /// <summary>
        /// eg. "Downs"
        /// </summary>
        public string? DisplayResult { get; set; }

        /// <summary>
        /// Number of yards gained or lost during the drive.
        /// </summary>
        public int Yards { get; set; }

        /// <summary>
        /// Gets or sets the total number of offensive plays executed.
        /// </summary>
        public int OffensivePlays { get; set; }

        /// <summary>
        /// Whether the drive resulted in a score.
        /// </summary>
        public bool IsScore { get; set; }

        public string? SourceId { get; set; }

        public string? SourceDescription { get; set; }

        /// <summary>
        /// eg. Quarter
        /// </summary>
        public string? StartPeriodType { get; set; }

        /// <summary>
        /// eg. 1, 2, 3, 4, OT
        /// </summary>
        public int? StartPeriodNumber { get; set; }

        /// <summary>
        /// eg. 900.0
        /// </summary>
        public double? StartClockValue { get; set; }

        /// <summary>
        /// eg. "15:00"
        /// </summary>
        public string? StartClockDisplayValue { get; set; }

        public int? StartYardLine { get; set; }

        public string? StartText { get; set; }

        public int? StartDown { get; set; }

        public int? StartDistance { get; set; }

        public int? StartYardsToEndzone { get; set; }

        /// <summary>
        /// FranchiseSeasonId of the team that started the drive.
        /// </summary>
        public Guid? StartFranchiseSeasonId { get; set; }

        public string? StartDownDistanceText { get; set; }

        public string? StartShortDownDistanceText { get; set; }

        /// <summary>
        /// eg. Quarter
        /// </summary>
        public string? EndPeriodType { get; set; }

        /// <summary>
        /// eg. 1, 2, 3, 4
        /// </summary>
        public int? EndPeriodNumber { get; set; }

        /// <summary>
        /// eg. 466.0
        /// </summary>
        public double? EndClockValue { get; set; }

        /// <summary>
        /// eg. "7:46"
        /// </summary>
        public string? EndClockDisplayValue { get; set; }

        public int? EndYardLine { get; set; }

        public string? EndText { get; set; }

        public int? EndDown { get; set; }

        public int? EndDistance { get; set; }

        public int? EndYardsToEndzone { get; set; }

        /// <summary>
        /// FranchiseSeasonId of the team that ended the drive.
        /// </summary>
        public Guid? EndFranchiseSeasonId { get; set; }

        public string? EndDownDistanceText { get; set; }

        public string? EndShortDownDistanceText { get; set; }

        /// <summary>
        /// eg. "7:14"
        /// </summary>
        public string? TimeElapsedDisplay { get; set; }

        /// <summary>
        /// eg. 434.0
        /// </summary>
        public double? TimeElapsedValue { get; set; }

        public ICollection<Play> Plays { get; set; } = new List<Play>();
        
        public ICollection<DriveExternalId> ExternalIds { get; set; } = new List<DriveExternalId>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<Drive>
        {
            public void Configure(EntityTypeBuilder<Drive> builder)
            {
                builder.ToTable(nameof(Drive));
                builder.HasKey(x => x.Id);
                builder.Property(x => x.Description).IsRequired().HasMaxLength(250);
                builder.Property(x => x.SequenceNumber).IsRequired().HasMaxLength(20);
                builder.Property(x => x.Ordinal).IsRequired();
                builder.Property(x => x.Result).HasMaxLength(50);
                builder.Property(x => x.ShortDisplayResult).HasMaxLength(50);
                builder.Property(x => x.DisplayResult).HasMaxLength(50);
                builder.Property(x => x.SourceId).HasMaxLength(20);
                builder.Property(x => x.SourceDescription).HasMaxLength(100);
                builder.Property(x => x.StartText).HasMaxLength(100);
                builder.Property(x => x.StartClockDisplayValue).HasMaxLength(20);
                builder.Property(x => x.StartDownDistanceText).HasMaxLength(50);
                builder.Property(x => x.StartShortDownDistanceText).HasMaxLength(50);
                builder.Property(x => x.EndText).HasMaxLength(100);
                builder.Property(x => x.EndClockDisplayValue).HasMaxLength(20);
                builder.Property(x => x.EndDownDistanceText).HasMaxLength(50);
                builder.Property(x => x.EndShortDownDistanceText).HasMaxLength(50);
                builder.Property(x => x.TimeElapsedDisplay).HasMaxLength(20);

                builder.HasMany(x => x.Plays)
                    .WithOne(x => x.Drive)
                    .HasForeignKey(x => x.DriveId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.Property(x => x.CompetitionId).IsRequired();

                builder.HasOne(x => x.Competition)
                    .WithMany(x => x.Drives)
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.ExternalIds)
                    .WithOne()
                    .HasForeignKey(x => x.DriveId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
