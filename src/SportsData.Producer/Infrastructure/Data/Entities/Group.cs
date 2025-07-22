using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Common;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Group : CanonicalEntityBase<Guid>, IHasSlug, IHasExternalIds
    {
        public required string Name { get; set; }

        public required string Abbreviation { get; set; }

        public required string ShortName { get; set; }

        public required string MidsizeName { get; set; }

        public required string Slug { get; set; }

        public bool IsConference { get; set; } = true;

        public Guid? ParentGroupId { get; set; }

        public ICollection<GroupSeason> Seasons { get; set; } = new List<GroupSeason>();

        public ICollection<GroupExternalId> ExternalIds { get; set; } = new List<GroupExternalId>();

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public ICollection<GroupLogo> Logos { get; set; } = new List<GroupLogo>();

        public class EntityConfiguration : IEntityTypeConfiguration<Group>
        {
            public void Configure(EntityTypeBuilder<Group> builder)
            {
                builder.ToTable(nameof(Group));
                builder.HasKey(t => t.Id);

                builder.Property(t => t.Name)
                    .IsRequired()
                    .HasMaxLength(150);

                builder.Property(t => t.Abbreviation)
                    .IsRequired()
                    .HasMaxLength(50);

                builder.Property(t => t.ShortName)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.MidsizeName)
                    .IsRequired()
                    .HasMaxLength(100);

                builder.Property(t => t.Slug)
                    .IsRequired()
                    .HasMaxLength(100);
            }
        }

    }
}