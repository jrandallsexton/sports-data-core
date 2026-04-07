using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class Draft : CanonicalEntityBase<Guid>
{
    public int Year { get; set; }

    public int NumberOfRounds { get; set; }

    public string? DisplayName { get; set; }

    public string? ShortDisplayName { get; set; }

    public ICollection<DraftRound> Rounds { get; set; } = new List<DraftRound>();

    public class EntityConfiguration : IEntityTypeConfiguration<Draft>
    {
        public void Configure(EntityTypeBuilder<Draft> builder)
        {
            builder.ToTable(nameof(Draft));
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.Year).IsRequired();
            builder.Property(t => t.NumberOfRounds).IsRequired();
            builder.Property(t => t.DisplayName).HasMaxLength(150);
            builder.Property(t => t.ShortDisplayName).HasMaxLength(50);

            builder.HasIndex(t => t.Year).IsUnique();

            builder.HasMany(t => t.Rounds)
                .WithOne(r => r.Draft)
                .HasForeignKey(r => r.DraftId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
