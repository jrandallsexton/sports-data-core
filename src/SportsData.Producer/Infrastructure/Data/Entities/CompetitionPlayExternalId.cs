using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class CompetitionPlayExternalId : ExternalId
{
    public Guid CompetitionPlayId { get; set; }

    public CompetitionPlay CompetitionPlay { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<CompetitionPlayExternalId>
    {
        public void Configure(EntityTypeBuilder<CompetitionPlayExternalId> builder)
        {
            builder.ToTable(nameof(CompetitionPlayExternalId));
            builder.HasKey(t => t.Id);
            builder.HasOne(t => t.CompetitionPlay)
                .WithMany(v => v.ExternalIds)
                .HasForeignKey(t => t.CompetitionPlayId);
        }
    }
}