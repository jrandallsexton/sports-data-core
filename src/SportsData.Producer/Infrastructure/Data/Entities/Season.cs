using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Season : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public int Year { get; set; }

        public required string Name { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public List<SeasonPhase> Phases { get; set; } = [];

        public Guid? ActivePhaseId { get; set; }

        public SeasonPhase? ActivePhase { get; set; }

        public List<SeasonExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<Season>
        {
            public void Configure(EntityTypeBuilder<Season> builder)
            {
                builder.ToTable(nameof(Season));

                builder.HasKey(t => t.Id);
                builder.Property(p => p.Id).ValueGeneratedNever();

                builder.Property(t => t.Name)
                    .HasMaxLength(100)
                    .IsRequired();

                builder.HasMany(s => s.Phases)
                    .WithOne(p => p.Season)
                    .HasForeignKey(p => p.SeasonId);

                builder.HasOne(s => s.ActivePhase)
                    .WithMany()
                    .HasForeignKey(s => s.ActivePhaseId)
                    .OnDelete(DeleteBehavior.Restrict);

                builder.HasMany(s => s.ExternalIds)
                    .WithOne(eid => eid.Season)
                    .HasForeignKey(eid => eid.SeasonId)
                    .OnDelete(DeleteBehavior.Cascade);
            }

        }
    }
}
