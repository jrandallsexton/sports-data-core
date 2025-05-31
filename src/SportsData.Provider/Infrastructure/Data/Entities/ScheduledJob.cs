using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    public class ScheduledJob : CanonicalEntityBase<Guid>
    {
        public SourcingExecutionMode ExecutionMode { get; set; }

        public string Href { get; set; } = null!;

        public SourceDataProvider SourceDataProvider { get; set; }

        public Sport Sport { get; set; }

        public DocumentType DocumentType { get; set; }

        public int? SeasonYear { get; set; }

        public DateTime StartUtc { get; set; }

        public DateTime? EndUtc { get; set; }

        public int? PollingIntervalInSeconds { get; set; }

        public int? MaxAttempts { get; set; }

        public DateTime? TimeoutAfterUtc { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime? LastEnqueuedUtc { get; set; }

        public DateTime? LastCompletedUtc { get; set; }

        public int? LastPageIndex { get; set; }

        public int? TotalPageCount { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<ScheduledJob>
        {
            public void Configure(EntityTypeBuilder<ScheduledJob> builder)
            {
                builder.ToTable("ScheduledJob");

                builder.HasKey(t => t.Id);

                builder.HasIndex(t => new { t.IsActive, t.StartUtc, t.EndUtc })
                    .HasDatabaseName("IX_ScheduledJob_IsActive_StartUtc_EndUtc");

                builder.HasIndex(t => t.Href)
                    .HasDatabaseName("IX_ScheduledJob_Href");

                builder.HasIndex(t => new { t.SourceDataProvider, t.Sport, t.DocumentType })
                    .HasDatabaseName("IX_ScheduledJob_SourceDataProvider_Sport_DocumentType");

                builder.HasIndex(t => t.ExecutionMode)
                    .HasDatabaseName("IX_ScheduledJob_ExecutionMode");

                builder.Property(t => t.Href)
                    .HasMaxLength(1024)
                    .IsRequired();
            }
        }

    }

    public enum SourcingExecutionMode
    {
        OneTime,
        PollUntilConditionMet
    }

}
