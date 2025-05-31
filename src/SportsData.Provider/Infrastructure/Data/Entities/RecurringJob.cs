using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    public class RecurringJob : CanonicalEntityBase<Guid>
    {
        public int Ordinal { get; set; }

        public string Name { get; set; }

        public bool IsRecurring { get; set; }

        public string? CronExpression { get; set; }

        public bool IsEnabled { get; set; }

        public SourceDataProvider Provider { get; set; }

        public DocumentType DocumentType { get; set; }

        public Sport SportId { get; set; }

        public string Endpoint { get; set; }

        public string? EndpointMask { get; set; }

        public bool IsSeasonSpecific { get; set; }

        public int? SeasonYear { get; set; }

        public DateTime? LastAccessed { get; set; }

        public int? LastPageIndex { get; set; }

        public int? TotalPageCount { get; set; }

        public List<ResourceIndexItem> Items { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<RecurringJob>
        {
            public void Configure(EntityTypeBuilder<RecurringJob> builder)
            {
                builder.ToTable("RecurringJob");
                builder.HasKey(t => t.Id);

                builder.HasIndex(t => new { t.IsEnabled, t.Provider, t.SportId, t.DocumentType, t.SeasonYear })
                    .HasDatabaseName("IX_RecurringJob_Enabled_Provider_Sport_DocumentType_Season");

                builder.HasIndex(t => t.Endpoint)
                    .HasDatabaseName("IX_RecurringJob_Endpoint");

                builder.HasIndex(t => t.LastAccessed)
                    .HasDatabaseName("IX_RecurringJob_LastAccessed");
            }
        }

    }
}
