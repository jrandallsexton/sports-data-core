using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class GroupSeason : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid? ParentId { get; set; }

        public GroupSeason? Parent { get; set; }

        public Guid? SeasonId { get; set; }

        public Season? Season { get; set; }

        public int SeasonYear { get; set; }

        public required string Name { get; set; }

        public required string Slug { get; set; }

        public required string Abbreviation { get; set; }

        public required string ShortName { get; set; }

        public string? MidsizeName { get; set; }

        public bool IsConference { get; set; } = false;

        public List<GroupSeasonLogo> Logos { get; set; } = [];

        public ICollection<GroupSeason> Children { get; set; } = [];

        public ICollection<FranchiseSeason> FranchiseSeasons { get; set; } = [];

        public ICollection<GroupSeasonExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public class EntityConfiguration : IEntityTypeConfiguration<GroupSeason>
        {
            public void Configure(EntityTypeBuilder<GroupSeason> builder)
            {
                builder.ToTable(nameof(GroupSeason));

                builder.HasKey(t => t.Id);

                builder.Property(t => t.Name)
                    .IsRequired()
                    .HasMaxLength(200);

                builder.Property(t => t.Slug)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.Abbreviation)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(t => t.ShortName)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.MidsizeName)
                    .HasMaxLength(100);

                builder.HasMany(x => x.Logos)
                    .WithOne()
                    .HasForeignKey(x => x.GroupSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.ExternalIds)
                    .WithOne()
                    .HasForeignKey(x => x.GroupSeasonId)
                    .OnDelete(DeleteBehavior.Cascade);

                builder.HasMany(x => x.FranchiseSeasons)
                    .WithOne(x => x.GroupSeason)
                    .HasForeignKey(x => x.GroupSeasonId)
                    .OnDelete(DeleteBehavior.Restrict); // or Cascade, depending on your intent

                builder
                    .HasOne(x => x.Parent)
                    .WithMany(x => x.Children)
                    .HasForeignKey(x => x.ParentId)
                    .OnDelete(DeleteBehavior.Restrict); // avoid cascade cycles

                // (optional but recommended)
                builder.HasIndex(x => x.ParentId);
                builder.HasIndex(x => new { x.SeasonYear, x.Slug }).IsUnique(false);

                // --- NEW: Season relation (no back-collection on Season) ---
                builder
                    .HasOne(x => x.Season)
                    .WithMany()                         // no navigation on Season
                    .HasForeignKey(x => x.SeasonId)
                    .OnDelete(DeleteBehavior.Restrict); // keep history / avoid cascades
            }
        }

    }
}
