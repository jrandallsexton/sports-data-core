using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
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

        public List<GroupSeason> Seasons { get; set; } = [];

        public List<GroupExternalId> ExternalIds { get; set; } = [];

        public IEnumerable<ExternalId> GetExternalIds() => ExternalIds;

        public List<GroupLogo> Logos { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Group>
        {
            public void Configure(EntityTypeBuilder<Group> builder)
            {
                builder.ToTable("Group");
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