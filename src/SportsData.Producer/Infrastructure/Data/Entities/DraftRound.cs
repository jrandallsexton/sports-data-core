using System;
using System.Collections.Generic;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class DraftRound : CanonicalEntityBase<Guid>
{
    public Guid DraftId { get; set; }

    public Draft Draft { get; set; } = default!;

    public int Number { get; set; }

    public string? DisplayName { get; set; }

    public string? ShortDisplayName { get; set; }

    public ICollection<DraftPick> Picks { get; set; } = new List<DraftPick>();

    public class EntityConfiguration : IEntityTypeConfiguration<DraftRound>
    {
        public void Configure(EntityTypeBuilder<DraftRound> builder)
        {
            builder.ToTable(nameof(DraftRound));
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.Number).IsRequired();
            builder.Property(t => t.DisplayName).HasMaxLength(50);
            builder.Property(t => t.ShortDisplayName).HasMaxLength(20);

            builder.HasIndex(t => new { t.DraftId, t.Number }).IsUnique();

            builder.HasMany(t => t.Picks)
                .WithOne(p => p.DraftRound)
                .HasForeignKey(p => p.DraftRoundId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
