using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;
using SportsData.Producer.Infrastructure.Data.Entities.Contracts;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class Group : CanonicalEntityBase<Guid>, IHasSlug
    {
        public string Name { get; set; }

        public string Abbreviation { get; set; }

        public string ShortName { get; set; }

        public string MidsizeName { get; set; }

        public string Slug { get; set; }

        public bool IsConference { get; set; } = true;

        public Guid? ParentGroupId { get; set; }

        public List<GroupSeason> Seasons { get; set; }

        public List<GroupExternalId> ExternalIds { get; set; }

        public List<GroupLogo> Logos { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<Group>
        {
            public void Configure(EntityTypeBuilder<Group> builder)
            {
                builder.ToTable("Group");
                builder.HasKey(t => t.Id);
            }
        }
    }
}