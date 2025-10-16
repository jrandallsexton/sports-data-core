using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class CompetitionSituation : CanonicalEntityBase<Guid>
    {
        public Guid CompetitionId { get; set; }
        public Competition Competition { get; set; } = null!;

        public Guid? LastPlayId { get; set; }

        public CompetitionPlay? LastPlay { get; set; }

        public int Down { get; set; }

        public int Distance { get; set; }

        public int YardLine { get; set; }

        public bool IsRedZone { get; set; }

        public int AwayTimeouts { get; set; }

        public int HomeTimeouts { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<CompetitionSituation>
        {
            public void Configure(EntityTypeBuilder<CompetitionSituation> builder)
            {
                builder.ToTable("CompetitionSituation");

                builder.HasKey(x => x.Id);

                // -------- Parent: Competition (required) --------
                builder.HasOne(x => x.Competition)
                       .WithMany(c => c.Situations)
                       .HasForeignKey(x => x.CompetitionId)
                       .OnDelete(DeleteBehavior.Restrict);

                builder.HasIndex(x => x.CompetitionId);

                // -------- Optional: LastPlay --------
                builder.HasOne(x => x.LastPlay)
                    .WithMany(p => p.SituationsAsLastPlay)
                    .HasForeignKey(x => x.LastPlayId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasIndex(x => x.LastPlayId);

                // -------- Property rules --------
                builder.Property(x => x.Down).IsRequired();
                builder.Property(x => x.Distance).IsRequired();
                builder.Property(x => x.YardLine).IsRequired();
                builder.Property(x => x.IsRedZone).IsRequired();
                builder.Property(x => x.AwayTimeouts).IsRequired();
                builder.Property(x => x.HomeTimeouts).IsRequired();

                // -------- Check constraints (PostgreSQL-friendly) --------
                builder.ToTable(t =>
                {
                    // -1|0..4 downs (-1|0 = no down, 1-4 = valid downs) -1|0 at end of game
                    t.HasCheckConstraint("CK_CompetitionSituation_Down", "\"Down\" BETWEEN -1 AND 4");
                    // 0..100 yard line (covers goal line/touchback edges)
                    t.HasCheckConstraint("CK_CompetitionSituation_YardLine", "\"YardLine\" BETWEEN 0 AND 100");
                    // Distance >= 0
                    t.HasCheckConstraint("CK_CompetitionSituation_Distance", "\"Distance\" >= -110");
                    // Timeouts >= 0 (tighten to 0..3 if you want to enforce NCAA max)
                    t.HasCheckConstraint("CK_CompetitionSituation_AwayTimeouts", "\"AwayTimeouts\" >= 0");
                    t.HasCheckConstraint("CK_CompetitionSituation_HomeTimeouts", "\"HomeTimeouts\" >= 0");
                });
            }
        }
    }
}
