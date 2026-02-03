using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities;

public class AthleteSeasonInjury : CanonicalEntityBase<Guid>
{
    public Guid AthleteSeasonId { get; set; }
    public AthleteSeason AthleteSeason { get; set; } = default!;

    public required string TypeId { get; set; }
    public required string Type { get; set; }
    public string? TypeDescription { get; set; }
    public string? TypeAbbreviation { get; set; }

    public DateTime Date { get; set; }

    public required string Headline { get; set; }

    public required string Text { get; set; }

    public string? Source { get; set; }

    public string? Status { get; set; }

    /// <summary>
    /// Concurrency token using PostgreSQL's xmin system column.
    /// EF Core automatically updates this on every SaveChanges and checks it for conflicts.
    /// </summary>
    public uint RowVersion { get; set; }

    public class EntityConfiguration : IEntityTypeConfiguration<AthleteSeasonInjury>
    {
        public void Configure(EntityTypeBuilder<AthleteSeasonInjury> builder)
        {
            builder.ToTable(nameof(AthleteSeasonInjury));
            builder.HasKey(x => x.Id);

            builder.Property(x => x.AthleteSeasonId).IsRequired();
            builder.Property(x => x.TypeId).IsRequired().HasMaxLength(10);
            builder.Property(x => x.Type).IsRequired().HasMaxLength(50);
            builder.Property(x => x.TypeDescription).HasMaxLength(100);
            builder.Property(x => x.TypeAbbreviation).HasMaxLength(10);
            builder.Property(x => x.Date).IsRequired();
            builder.Property(x => x.Headline).IsRequired().HasMaxLength(500);
            builder.Property(x => x.Text).IsRequired().HasMaxLength(2000);
            builder.Property(x => x.Source).HasMaxLength(100);
            builder.Property(x => x.Status).HasMaxLength(50);

            builder.Property(t => t.RowVersion)
                .IsRowVersion();

            builder.HasOne(x => x.AthleteSeason)
                .WithMany()
                .HasForeignKey(x => x.AthleteSeasonId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(x => x.AthleteSeasonId);
        }
    }
}
