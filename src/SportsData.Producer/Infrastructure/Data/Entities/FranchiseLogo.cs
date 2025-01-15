using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using SportsData.Core.Infrastructure.Data.Entities;

namespace SportsData.Producer.Infrastructure.Data.Entities
{
    public class FranchiseLogo : EntityBase<Guid>
    {
        public string Url { get; set; }

        public int Height { get; set; }

        public int Width { get; set; }

        public List<string> Rel { get; set; } = [];

        public class EntityConfiguration : IEntityTypeConfiguration<FranchiseLogo>
        {
            public void Configure(EntityTypeBuilder<FranchiseLogo> builder)
            {
                builder.ToTable("FranchiseLogo");
                builder.HasKey(t => t.Id);
            }
        }
    }
}