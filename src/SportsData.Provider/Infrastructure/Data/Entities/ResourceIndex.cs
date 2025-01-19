﻿using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Provider.Infrastructure.Data.Entities
{
    public class ResourceIndex : EntityBase<Guid>
    {
        public bool IsRecurring { get; set; }

        public SourceDataProvider ProviderId { get; set; }

        public DocumentType DocumentTypeId { get; set; }

        public Sport SportId { get; set; }

        public string Endpoint { get; set; }

        public string EndpointMask { get; set; }

        public int? SeasonYear { get; set; }

        public DateTime? LastAccessed { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<ResourceIndex>
        {
            public void Configure(EntityTypeBuilder<ResourceIndex> builder)
            {
                builder.ToTable("ResourceIndex");
                builder.HasKey(t => t.Id);
            }
        }
    }
}
