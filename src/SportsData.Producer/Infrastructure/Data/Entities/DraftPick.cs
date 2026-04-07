using System;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class DraftPick : CanonicalEntityBase<Guid>
{
    public Guid DraftRoundId { get; set; }

    public DraftRound DraftRound { get; set; } = default!;

    public int Pick { get; set; }

    public int Overall { get; set; }

    public bool Traded { get; set; }

    public string? TradeNote { get; set; }

    public string? AthleteRef { get; set; }

    public string? TeamRef { get; set; }

    public string? StatusName { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<DraftPick>
    {
        public void Configure(EntityTypeBuilder<DraftPick> builder)
        {
            builder.ToTable(nameof(DraftPick));
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Id).ValueGeneratedNever();

            builder.Property(t => t.Pick).IsRequired();
            builder.Property(t => t.Overall).IsRequired();
            builder.Property(t => t.Traded).IsRequired();

            builder.Property(t => t.TradeNote).HasMaxLength(200);
            builder.Property(t => t.AthleteRef).HasMaxLength(250);
            builder.Property(t => t.TeamRef).HasMaxLength(250);
            builder.Property(t => t.StatusName).HasMaxLength(50);

            builder.HasIndex(t => new { t.DraftRoundId, t.Pick });
        }
    }
}
