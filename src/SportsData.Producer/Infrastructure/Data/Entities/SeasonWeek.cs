using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class SeasonWeek : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid SeasonId { get; set; }

        public Season? Season { get; set; }

        public Guid SeasonPhaseId { get; set; }

        public SeasonPhase SeasonPhase { get; set; } = null!;

        public int Number { get; set; }

        public DateTime StartDate { get; set; }

        public DateTime EndDate { get; set; }

        public ICollection<SeasonPollWeek> Rankings { get; set; } = [];

        public ICollection<SeasonWeekExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<SeasonWeek>
        {
            public void Configure(EntityTypeBuilder<SeasonWeek> builder)
            {
                builder.ToTable("SeasonWeek");

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Number)
                    .IsRequired();

                builder.Property(x => x.StartDate)
                    .IsRequired();

                builder.Property(x => x.EndDate)
                    .IsRequired();

                builder.HasOne(x => x.Season)
                    .WithMany()
                    .HasForeignKey(x => x.SeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasOne(x => x.SeasonPhase)
                    .WithMany(p => p.Weeks)
                    .HasForeignKey(x => x.SeasonPhaseId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.ExternalIds)
                    .WithOne()
                    .HasForeignKey(x => x.SourceUrlHash)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.Navigation(x => x.ExternalIds)
                    .AutoInclude(false);

                builder.HasMany(x => x.Rankings)
                    .WithOne(x => x.SeasonWeek)
                    .HasForeignKey(x => x.SeasonWeekId)
                    .OnDelete(DeleteBehavior.Cascade); // or Restrict, depending on your deletion rules
            }
        }
    }
}
