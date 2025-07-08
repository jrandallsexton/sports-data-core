using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonFutureBook : CanonicalEntityBase<Guid>
{
    public Guid SeasonFutureItemId { get; set; }

    public SeasonFutureItem SeasonFutureItem { get; set; } = null!;

    public Guid FranchiseSeasonId { get; set; }

    public FranchiseSeason FranchiseSeason { get; set; } = null!;

    public required string Value { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonFutureBook>
    {
        public void Configure(EntityTypeBuilder<SeasonFutureBook> builder)
        {
            builder.ToTable("SeasonFutureBook");

            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.Value)
                .HasMaxLength(20)
                .IsRequired();

            builder.HasOne(t => t.SeasonFutureItem)
                .WithMany(t => t.Books)
                .HasForeignKey(t => t.SeasonFutureItemId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.FranchiseSeason)
                .WithMany()
                .HasForeignKey(t => t.FranchiseSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(t => t.SeasonFutureItemId);
            builder.HasIndex(t => t.FranchiseSeasonId);
        }
    }
}