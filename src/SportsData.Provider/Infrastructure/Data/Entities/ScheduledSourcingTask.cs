using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    public class ScheduledSourcingTask : CanonicalEntityBase<Guid>
    {
        public string Href { get; set; } = null!;

        public SourceDataProvider SourceDataProvider { get; set; }

        public Sport Sport { get; set; }

        public DocumentType DocumentType { get; set; }

        public int? SeasonYear { get; set; }

        public DateTime StartUtc { get; set; }

        public DateTime? EndUtc { get; set; }

        public int PollingIntervalInSeconds { get; set; } = 60;

        public bool IsActive { get; set; } = true;

        public DateTime? LastEnqueuedUtc { get; set; }

        public DateTime? LastCompletedUtc { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<ScheduledSourcingTask>
        {
            public void Configure(EntityTypeBuilder<ScheduledSourcingTask> builder)
            {
                builder.ToTable("ScheduledSourcingTask");
                builder.HasKey(t => t.Id);

                builder.HasIndex(t => new { t.IsActive, t.StartUtc, t.EndUtc })
                    .HasDatabaseName("IX_ScheduledSourcingTasks_IsActive_StartUtc_EndUtc");

                builder.HasIndex(t => t.Href)
                    .HasDatabaseName("IX_ScheduledSourcingTasks_Href");

                builder.HasIndex(t => new { t.SourceDataProvider, t.Sport, t.DocumentType })
                    .HasDatabaseName("IX_ScheduledSourcingTasks_SourceDataProvider_Sport_DocumentType");
            }
        }

    }
}
