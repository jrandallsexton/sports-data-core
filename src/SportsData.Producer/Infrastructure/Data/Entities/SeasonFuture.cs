using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonFuture : CanonicalEntityBase<Guid>, IHasExternalIds
{
    public Guid SeasonId { get; set; }

    public Season Season { get; set; } = null!;

    public required string Name { get; set; }

    public string? Type { get; set; }

    public string? DisplayName { get; set; }

    public ICollection<SeasonFutureItem> Items { get; set; } = new List<SeasonFutureItem>();

    public ICollection<SeasonFutureExternalId> ExternalIds { get; set; } = new List<SeasonFutureExternalId>();

    public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonFuture>
    {
        public void Configure(EntityTypeBuilder<SeasonFuture> builder)
        {
            builder.ToTable("SeasonFuture");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.Name)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(t => t.Type)
                .HasMaxLength(50);

            builder.Property(t => t.DisplayName)
                .HasMaxLength(100);

            builder.HasOne(t => t.Season)
                .WithMany()
                .HasForeignKey(t => t.SeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.Items)
                .WithOne(i => i.SeasonFuture)
                .HasForeignKey(i => i.SeasonFutureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.ExternalIds)
                .WithOne(e => e.SeasonFuture)
                .HasForeignKey(e => e.SeasonFutureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(t => t.SeasonId);
        }
    }
}