using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class SeasonPollWeekEntryStat : CanonicalEntityBase<Guid>
{
    public Guid SeasonPollWeekEntryId { get; set; }
    public SeasonPollWeekEntry PollWeekEntry { get; set; } = null!;

    public string Name { get; set; } = null!;             // "wins"
    public string DisplayName { get; set; } = null!;      // "Wins"
    public string ShortDisplayName { get; set; } = null!; // "W"
    public string Description { get; set; } = null!;
    public string Abbreviation { get; set; } = null!;     // "W"
    public string Type { get; set; } = null!;             // "wins"
    public decimal? Value { get; set; }                    // 0
    public string DisplayValue { get; set; } = null!;     // "0"

    public class EntityConfiguration : IEntityTypeConfiguration<SeasonPollWeekEntryStat>
    {
        public void Configure(EntityTypeBuilder<SeasonPollWeekEntryStat> builder)
        {
            builder.ToTable(nameof(SeasonPollWeekEntryStat));

            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever();

            // Strings
            builder.Property(s => s.Name).IsRequired().HasMaxLength(64);
            builder.Property(s => s.DisplayName).IsRequired().HasMaxLength(64);
            builder.Property(s => s.ShortDisplayName).IsRequired().HasMaxLength(16);
            builder.Property(s => s.Description).IsRequired().HasMaxLength(512);
            builder.Property(s => s.Abbreviation).IsRequired().HasMaxLength(16);
            builder.Property(s => s.Type).IsRequired().HasMaxLength(32);
            builder.Property(s => s.DisplayValue).IsRequired().HasMaxLength(32);

            // Numeric
            builder.Property(s => s.Value).HasPrecision(10, 2); // Provider-agnostic

            // Relationships
            builder.HasOne(s => s.PollWeekEntry)
                .WithMany(e => e.Stats)
                .HasForeignKey(s => s.SeasonPollWeekEntryId)
                .OnDelete(DeleteBehavior.Cascade);

            // Avoid duplicate stats per entry (e.g., wins/losses)
            builder.HasIndex(s => new { s.SeasonPollWeekEntryId, s.Name, s.Type }).IsUnique();
        }
    }

}