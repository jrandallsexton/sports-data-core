using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class SeasonPhaseExternalId : ExternalId
    {
        public Guid SeasonPhaseId { get; set; }

        public SeasonPhase? SeasonPhase { get; set; }

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonPhaseExternalId>
        {
            public void Configure(EntityTypeBuilder<SeasonPhaseExternalId> builder)
            {
                builder.ToTable("SeasonPhaseExternalId");
                builder.HasKey(e => e.Id);
                builder.Property(e => e.Id).ValueGeneratedNever();

                builder.Property(e => e.Value)
                    .HasMaxLength(100)
                    .IsRequired();

                builder.Property(e => e.SourceUrlHash)
                    .HasMaxLength(256)
                    .IsRequired();

                builder.Property(e => e.Provider)
                    .IsRequired();

                builder.HasOne(e => e.SeasonPhase)
                    .WithMany(p => p.ExternalIds)
                    .HasForeignKey(e => e.SeasonPhaseId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}