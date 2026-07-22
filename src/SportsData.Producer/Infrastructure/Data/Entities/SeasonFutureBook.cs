using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonFutureBook : CanonicalEntityBase<Guid>
{
    public Guid SeasonFutureItemId { get; set; }

    public SeasonFutureItem SeasonFutureItem { get; set; } = null!;

    // A book is scoped to EITHER a team (most futures) OR an athlete (season
    // MVP, awards, etc.). Exactly one of these is set. Athlete-market books were
    // previously discarded because only the team FK existed.
    public Guid? FranchiseSeasonId { get; set; }

    public FranchiseSeason? FranchiseSeason { get; set; }

    public Guid? AthleteSeasonId { get; set; }

    public AthleteSeason? AthleteSeason { get; set; }

    public required string Value { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonFutureBook>
    {
        public void Configure(EntityTypeBuilder<SeasonFutureBook> builder)
        {
            builder.ToTable(nameof(SeasonFutureBook));

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

            builder.HasOne(t => t.AthleteSeason)
                .WithMany()
                .HasForeignKey(t => t.AthleteSeasonId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(t => t.SeasonFutureItemId);
            builder.HasIndex(t => t.FranchiseSeasonId);
            builder.HasIndex(t => t.AthleteSeasonId);
        }
    }
}