using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonFutureItem : CanonicalEntityBase<Guid>
{
    public Guid SeasonFutureId { get; set; }

    public SeasonFuture SeasonFuture { get; set; } = null!;

    public required string ProviderId { get; set; }

    public required string ProviderName { get; set; }

    public int ProviderActive { get; set; }

    public int ProviderPriority { get; set; }

    public ICollection<SeasonFutureBook> Books { get; set; } = new List<SeasonFutureBook>();

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonFutureItem>
    {
        public void Configure(EntityTypeBuilder<SeasonFutureItem> builder)
        {
            builder.ToTable(nameof(SeasonFutureItem));

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.ProviderId)
                .HasMaxLength(50)
                .IsRequired();

            builder.Property(t => t.ProviderName)
                .HasMaxLength(100)
                .IsRequired();

            builder.Property(t => t.ProviderActive)
                .IsRequired();

            builder.Property(t => t.ProviderPriority)
                .IsRequired();

            builder.HasOne(t => t.SeasonFuture)
                .WithMany(t => t.Items)
                .HasForeignKey(t => t.SeasonFutureId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.Books)
                .WithOne(b => b.SeasonFutureItem)
                .HasForeignKey(b => b.SeasonFutureItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(t => t.SeasonFutureId);
        }
    }
}