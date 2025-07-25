using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Producer.Infrastructure.Data.Common;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonFutureExternalId : ExternalId
{
    public Guid SeasonFutureId { get; set; }
    public SeasonFuture SeasonFuture { get; set; } = null!;

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonFutureExternalId>
    {
        public void Configure(EntityTypeBuilder<SeasonFutureExternalId> builder)
        {
            builder.ToTable(nameof(SeasonFutureExternalId));
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();
            builder.HasOne(t => t.SeasonFuture)
                .WithMany(t => t.ExternalIds)
                .HasForeignKey(t => t.SeasonFutureId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Property(t => t.Value).HasMaxLength(256).IsRequired();
        }
    }
}