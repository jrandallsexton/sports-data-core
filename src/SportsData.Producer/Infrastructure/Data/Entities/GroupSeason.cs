using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class GroupSeason : CanonicalEntityBase<Guid>, IHasExternalIds
    {
        public Guid GroupId { get; set; }

        public Group Group { get; set; } = default!;

        public int Season { get; set; }

        public required string Name { get; set; }

        public required string Slug { get; set; }

        public required string Abbreviation { get; set; }

        public required string ShortName { get; set; }

        public string? MidsizeName { get; set; }

        public List<GroupSeasonLogo> Logos { get; set; } = [];

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

                builder.HasOne(x => x.Group)
                    .WithMany(x => x.Seasons)
                    .HasForeignKey(x => x.GroupId)
                    .OnDelete(DeleteBehavior.Cascade);

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

            }
        }

    }
}
