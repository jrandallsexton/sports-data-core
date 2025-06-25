using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Common;
using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Extensions;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Contest : CanonicalEntityBase<Guid>
    {
        public required string Name { get; set; }

        public required string ShortName { get; set; }

        public required DateTime StartDateUtc { get; set; }

        public DateTime? EndDateUtc { get; set; }

        public required ContestStatus Status { get; set; }

        public required Sport Sport { get; set; }

        public required int SeasonYear { get; set; }

        public int? SeasonType { get; set; }         // ESPN exposes this via seasonType ref

        public int? Week { get; set; }               // From `week` ref, parsed from URL or hydrated from companion doc

        public bool? NeutralSite { get; set; }

        public int? Attendance { get; set; }

        public string? EventNote { get; set; }       // e.g., "Modelo Vegas Kickoff Classic"

        public Guid? VenueId { get; set; }

        public List<ContestLink> Links { get; set; } = []; // Normalized set of rel/href for downstream use

        public List<ContestExternalId> ExternalIds { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Contest>
        {
            public void Configure(EntityTypeBuilder<Contest> builder)
            {
                builder.ToTable("Contests");

                builder.HasKey(x => x.Id);

                builder.Property(x => x.Name)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(x => x.ShortName)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(x => x.StartDateUtc)
                    .IsRequired();

                builder.Property(x => x.EndDateUtc);

                builder.Property(x => x.Status)
                    .IsRequired();

                builder.Property(x => x.Sport)
                    .IsRequired();

                builder.Property(x => x.SeasonYear)
                    .IsRequired();

                builder.Property(x => x.SeasonType);

                builder.Property(x => x.Week);

                builder.Property(x => x.NeutralSite);

                builder.Property(x => x.Attendance);

                builder.Property(x => x.EventNote)
                    .HasMaxLength(250);

                builder.Property(x => x.VenueId);

                builder
                    .HasMany(x => x.Links)
                    .WithOne(x => x.Contest)
                    .HasForeignKey(x => x.ContestId)
                    .OnDelete(DeleteBehavior.Cascade);
            }
        }
    }
}
