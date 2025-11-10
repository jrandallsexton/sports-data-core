using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Enums;
using SportsData.Producer.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data
{
    public class CompetitionStream : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        public Guid SeasonWeekId { get; set; }
        public SeasonWeek SeasonWeek { get; set; } = null!;

        public DateTime ScheduledTimeUtc { get; set; }

        public string BackgroundJobId { get; set; } = default!;

        public CompetitionStreamStatus Status { get; set; }

        public DateTime? StreamStartedUtc { get; set; }

        public DateTime? StreamEndedUtc { get; set; }

        public string? FailureReason { get; set; }

        public int RetryCount { get; set; }

        public string? ScheduledBy { get; set; }

        public string? Notes { get; set; }


        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionStream>
        {
            public void Configure(EntityTypeBuilder<CompetitionStream> builder)
            {
                builder.ToTable(nameof(CompetitionStream));

                builder.HasKey(x => x.Id);

                builder.Property(x => x.CompetitionId)
                    .IsRequired();

                builder.Property(x => x.SeasonWeekId)
                    .IsRequired();

                builder.Property(x => x.ScheduledTimeUtc)
                    .IsRequired();

                builder.Property(x => x.BackgroundJobId)
                    .HasMaxLength(64)
                    .IsRequired();

                builder.Property(x => x.Status)
                    .HasConversion<int>()
                    .IsRequired();

                builder.Property(x => x.StreamStartedUtc);

                builder.Property(x => x.StreamEndedUtc);

                builder.Property(x => x.FailureReason)
                    .HasMaxLength(512);

                builder.Property(x => x.RetryCount)
                    .IsRequired();

                builder.Property(x => x.ScheduledBy)
                    .HasMaxLength(128);

                builder.Property(x => x.Notes)
                    .HasMaxLength(1024);

                builder.HasIndex(x => x.CompetitionId)
                    .IsUnique();

                builder.HasOne(x => x.Competition)
                    .WithMany()
                    .HasForeignKey(x => x.CompetitionId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.SeasonWeek)
                    .WithMany()
                    .HasForeignKey(x => x.SeasonWeekId)
                    .OnDelete(DeleteBehavior.Cascade);

            }
        }
    }
}
