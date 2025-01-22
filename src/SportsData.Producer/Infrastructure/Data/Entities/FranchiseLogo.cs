using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseLogo : CanonicalEntityBase<Guid>, ILogo
    {
        public Guid FranchiseId { get; set; }

        public string Url { get; set; }

        public long? Height { get; set; }

        public long? Width { get; set; }

        public List<string>? Rel { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseLogo>
        {
            public void Configure(EntityTypeBuilder<FranchiseLogo> builder)
            {
                builder.ToTable("FranchiseLogo");
                builder.HasKey(t => t.Id);
                builder.HasOne<Franchise>()
                    .WithMany(x => x.Logos)
                    .HasForeignKey(x => x.FranchiseId);
            }
        }
    }
}