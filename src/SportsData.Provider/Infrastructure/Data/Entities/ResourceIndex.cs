using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Common.Hashing;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    public class ResourceIndex : CanonicalEntityBase<Guid>, IHasSourceUrlHash
    {
        public int Ordinal { get; set; }

        public required string Name { get; set; }

        public bool IsRecurring { get; set; }

        /// <summary>
        /// Is this job queued for execution and/or currently processing?
        /// </summary>
        public bool IsQueued { get; set; }

        public string? CronExpression { get; set; }

        public bool IsEnabled { get; set; }

        public SourceDataProvider Provider { get; set; }

        public DocumentType DocumentType { get; set; }

        public Sport SportId { get; set; }

        public required string Url { get; set; }

        public required string UrlHash { get; set; }

        public string? EndpointMask { get; set; }

        public bool IsSeasonSpecific { get; set; }

        public int? SeasonYear { get; set; }

        public DateTime? LastAccessedUtc { get; set; }

        public DateTime? LastCompletedUtc { get; set; }

        public int? LastPageIndex { get; set; }

        public int? TotalPageCount { get; set; }

        public List<ResourceIndexItem> Items { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<ResourceIndex>
        {
            public void Configure(EntityTypeBuilder<ResourceIndex> builder)
            {
                builder.ToTable("ResourceIndex");
                builder.HasKey(t => t.Id);

                builder.HasIndex(t => new { t.IsEnabled, t.Provider, t.SportId, t.DocumentType, t.SeasonYear })
                    .HasDatabaseName("IX_ResourceIndex_Enabled_Provider_Sport_DocumentType_Season");

                builder.HasIndex(t => t.Url)
                    .HasDatabaseName("IX_ResourceIndex_Endpoint");

                builder.HasIndex(t => t.LastAccessedUtc)
                    .HasDatabaseName("IX_ResourceIndex_LastAccessed");

                builder.Property(p => p.Url)
                    .HasMaxLength(255);

                builder.Property(p => p.UrlHash)
                    .HasMaxLength(64);

                builder.Property(p => p.EndpointMask)
                    .HasMaxLength(20);

                builder.Property(p => p.CronExpression)
                    .HasMaxLength(20);
            }
        }
    }
}
